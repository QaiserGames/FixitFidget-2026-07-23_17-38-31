#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Real prefab + TMP geometry checks, isolated from the open scene and player saves.
public static class TicketLayoutChecks
{
    [MenuItem("Fixit Fidget/Checks/Compact portrait tickets")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before checking ticket layout.");
        var scene = EditorSceneManager.NewPreviewScene();
        CustomerProfile profile = null;
        Texture2D texture = null;
        Sprite neutral = null, impatient = null;
        try
        {
            var host = new GameObject("Ticket checks");
            SceneManager.MoveGameObjectToScene(host, scene);
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(host.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(1920, 1080);
            var railRect = new GameObject("Rail", typeof(RectTransform)).GetComponent<RectTransform>();
            railRect.SetParent(canvas.transform, false);
            var manager = host.AddComponent<TicketRailUI>();
            Set(manager, "rail", railRect);
            Invoke(manager, "Start");
            var tickets = (List<JobTicket>)Get(manager, "ordered");
            var prefab = AssetDatabase.LoadAssetAtPath<JobTicket>("Assets/AssetsPrefabs/Ticket.prefab");
            Require(prefab != null, "The authored ticket prefab exists.");
            var fixture = new GameObject("Inactive customers");
            fixture.SetActive(false);
            fixture.transform.SetParent(host.transform);
            profile = ScriptableObject.CreateInstance<CustomerProfile>();
            profile.characterName = "Grace";
            texture = new Texture2D(4, 2);
            neutral = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            impatient = Sprite.Create(texture, new Rect(2, 0, 2, 2), Vector2.one * .5f);
            profile.portraitNeutral = neutral;
            profile.portraitAnnoyed = impatient;
            var owners = new List<CustomerBrain>();
            for (int i = 0; i < 6; i++)
            {
                var customer = new GameObject("Customer " + i);
                customer.transform.SetParent(fixture.transform);
                var brain = customer.AddComponent<CustomerBrain>();
                var identity = customer.AddComponent<CustomerIdentity>();
                if (i == 0) identity.SetupRegular(profile);
                else identity.SetupWalkIn(null, "Walk-in " + i);
                identity.Say(CustomerIdentity.Beat.Accepted);
                Set(brain, "identity", identity);
                Set(brain, "state", CustomerBrain.State.Waiting);
                Set(brain, "serviceMax", 100f);
                Set(brain, "patienceLeft", 80f);
                Set(brain, "record", new Job { deviceName = "Pocket Camera", faultDescription =
                    "Photographs fade halfway through. Preserve the family photograph while replacing the damaged contact." });
                Set(brain, "activeJob", customer.AddComponent<RepairJob>());
                var ticket = UnityEngine.Object.Instantiate(prefab, railRect);
                ticket.Bind(brain);
                tickets.Add(ticket);
                owners.Add(brain);
            }
            Invoke(manager, "LateUpdate");
            CheckRail(tickets, railRect, 1);
            Require(Mathf.Approximately(railRect.rect.height, 118), "Six tickets occupy only 118 pixels of height at 1920.");
            foreach (var ticket in tickets)
            {
                Require(Get<Image>(ticket, "background").color.a < .95f, "Card paper is translucent.");
                Require(Get<RectTransform>(ticket, "detailPanel") == null, "Full details are absent until requested.");
                Rect face = LocalRect(Get<Image>(ticket, "portraitFrame").rectTransform);
                Rect title = LocalRect(Get<TMP_Text>(ticket, "slotText").rectTransform);
                Rect body = LocalRect(Get<TMP_Text>(ticket, "jobText").rectTransform);
                Require(!face.Overlaps(title) && !face.Overlaps(body), "Portrait does not cover name or task.");
                Require(!body.Overlaps(LocalRect(Get<Image>(ticket, "patienceFill").rectTransform)), "Task does not cover patience.");
            }
            var first = tickets[0];
            Require(Get<Image>(first, "portrait").sprite == neutral, "Missing happy artwork falls back to neutral.");
            Require(Get<TMP_Text>(tickets[1], "portraitInitial").gameObject.activeSelf, "Walk-ins retain a readable initial.");
            Set(owners[0], "patienceLeft", 20f);
            Invoke(first, "Update");
            Require(Get<Image>(first, "portrait").sprite == impatient, "Accepted customer portrait follows live impatience.");
            Require(Get<TMP_Text>(first, "moodText").text == "Losing patience", "Urgency is readable without relying on face or colour.");
            Require(Mathf.Approximately(Get<Image>(first, "patienceFill").fillAmount, .2f), "Patience remains live.");

            var drink = AssetDatabase.LoadAssetAtPath<DrinkDefinition>("Assets/AssetsPrefabs/Drinks/Drink_HotChocolate.asset");
            Require(drink != null, "Long drink-name fixture exists.");
            Set(owners[0], "drinkOrdered", true);
            Set(owners[0], "drinkWish", drink);
            Invoke(first, "Update");
            var drinkLine = Get<TMP_Text>(first, "drinkText");
            Require(drinkLine.gameObject.activeSelf && drinkLine.text == "+ Hot Chocolate", "Long repair cannot hide the secondary drink.");
            Require(!LocalRect(drinkLine.rectTransform).Overlaps(LocalRect(Get<TMP_Text>(first, "jobText").rectTransform)), "Drink has a separate readable row.");
            CheckFits(drinkLine, "Longest current drink fits at six-card width.");
            Invoke(first, "RefreshDetails", (object)null);
            var detail = Get<TMP_Text>(first, "detailText");
            Require(detail.text.Contains(owners[0].Record.faultDescription) && detail.text.Contains("+ Hot Chocolate"),
                "Full task and constraint text remains available without abbreviation.");
            CheckFits(detail, "Full details grow to fit the authored task.");
            first.OnPointerExit(null);
            Require(!Get<RectTransform>(first, "detailPanel").gameObject.activeSelf, "Details close as soon as the pointer leaves.");

            var call = owners[0].gameObject.AddComponent<HoldCallJob>();
            Set(owners[0], "activeJob", call);
            var run = (HoldCallRun)Get(call, "run");
            run.Dial(1f, 15f); run.Tick(1.6f);
            Invoke(first, "Update");
            Require(Get<TMP_Text>(first, "callState").text == "Answer now"
                && Get<TMP_Text>(first, "callSeconds").text == "15s", "Urgent answer instruction and seconds remain explicit.");
            CheckFits(Get<TMP_Text>(first, "callState"), "Urgent call label fits.");
            CheckFits(Get<TMP_Text>(first, "callSeconds"), "Call countdown fits.");
            Require(!LocalRect(Get<RectTransform>(first, "callLine")).Overlaps(LocalRect(drinkLine.rectTransform)),
                "Support countdown does not cover the drink.");
            Require(!LocalRect(Get<RectTransform>(first, "callLine")).Overlaps(LocalRect(Get<Image>(first, "patienceFill").rectTransform)),
                "Support countdown does not cover patience.");
            Set(owners[0], "activeJob", null);
            Invoke(first, "Update");
            Require(!Get<RectTransform>(first, "callLine").gameObject.activeSelf
                && Get<TMP_Text>(first, "jobText").text == "" && drinkLine.gameObject.activeSelf,
                "Returning the phone clears its obligation and keeps the remaining drink.");

            canvasRect.sizeDelta = new Vector2(1280, 720);
            Invoke(manager, "LateUpdate");
            CheckRail(tickets, railRect, 3);
            Debug.Log("[Ticket layout] PASS: real prefab, six-card rail, narrow wrapping, live portrait/fallback, patience, separate drinks, full constraints, support countdown and handback. No scenes or saves changed.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
            if (neutral != null) UnityEngine.Object.DestroyImmediate(neutral);
            if (impatient != null) UnityEngine.Object.DestroyImmediate(impatient);
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void CheckRail(List<JobTicket> tickets, RectTransform rail, int expectedRows)
    {
        var rows = new HashSet<float>();
        for (int i = 0; i < tickets.Count; i++)
        {
            var rect = (RectTransform)tickets[i].transform;
            rows.Add(rect.anchoredPosition.y);
            Require(Mathf.Approximately(rect.rect.height, 118) && rect.rect.width >= 150, "Cards retain the compact readable minimum.");
            Rect bounds = LocalRect(rect);
            Require(bounds.xMin >= -rail.rect.width * .5f - .01f && bounds.xMax <= rail.rect.width * .5f + .01f,
                "Every ticket stays inside the available rail width.");
            for (int j = 0; j < i; j++) Require(!bounds.Overlaps(LocalRect((RectTransform)tickets[j].transform)), "Ticket cards do not overlap.");
        }
        Require(rows.Count == expectedRows, "Rail wraps only when the viewport needs it.");
    }
    private static void CheckFits(TMP_Text text, string message)
    {
        text.ForceMeshUpdate(true);
        Require(!text.isTextOverflowing, message);
    }
    private static Rect LocalRect(RectTransform rect) => new(rect.anchoredPosition + rect.rect.position, rect.rect.size);
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    private static T Get<T>(object target, string name) where T : class => Get(target, name) as T;
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Ticket layout: " + message); }
}
#endif
