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
// 26 Oct 2026 (HUD 3): the dockets (JobTicket, TicketRailUI): one row at any count, narrow from four, the face in its
// patience ring, the items as icons, the fault's words only on the wide docket; the old rows, mood words and drink line are gone.
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
            // Six tickets: one row of narrow dockets, 8 px apart, ending short of the window top centre.
            CheckRail(tickets, railRect, 1920, JobTicket.NarrowWidth, true);
            Require(manager.FormNow == JobTicket.Form.Narrow, "Six tickets are narrow dockets (from four, every ticket is).");
            Require(Mathf.Approximately(railRect.rect.height, TicketRailUI.TicketHeight), $"Six tickets occupy only {TicketRailUI.TicketHeight} pixels of height at 1920.");
            // The tabs sit in the top left corner (the HUD spec's), running right, and stop short of the window.
            Require(railRect.anchorMin == new Vector2(0, 1) && railRect.pivot == new Vector2(0, 1)
                && Mathf.Approximately(railRect.anchoredPosition.x, UiSkin.Margin), "The tabs start at the top left corner.");
            Require(UiSkin.Margin + railRect.rect.width <= TicketRailUI.RailEnd(1920) + .01f,
                "Six tabs stop short of the window top centre (16 px before its frame).");
            Require(Mathf.Approximately(TicketRailUI.BottomEdge, 20 + TicketRailUI.TicketHeight), "The guide under the tabs knows where they end.");
            foreach (var ticket in tickets)
            {
                Require(Get<Image>(ticket, "background").color.a < .95f, "Card paper is translucent.");
                Require(Get<RectTransform>(ticket, "detailPanel") == null, "Full details are absent until requested.");
                Rect ringRect = LocalRect(Get<RectTransform>(ticket, "ring"));
                Rect title = LocalRect(Get<TMP_Text>(ticket, "slotText").rectTransform);
                Rect icons = LocalRect(Get<RectTransform>(ticket, "iconsRow"));
                Require(!ringRect.Overlaps(title) && !ringRect.Overlaps(icons), "The face in its ring does not cover the name or the items.");
                Require(!title.Overlaps(icons), "The name does not cover the items.");
                Require(!Get<TMP_Text>(ticket, "jobText").gameObject.activeSelf, "A narrow docket carries no sentence (the details do).");
                var fill = Get<Image>(ticket, "patienceFill");
                Require(fill.type == Image.Type.Filled && fill.fillMethod == Image.FillMethod.Radial360, "Patience is the ring round the face.");
                Require(ticket.Items.Contains("item_"), $"The device shows as an icon ({ticket.Items}).");
            }
            var first = tickets[0];
            Require(Get<Image>(first, "portrait").sprite == neutral, "Missing happy artwork falls back to neutral.");
            Require(Get<TMP_Text>(tickets[1], "portraitInitial").gameObject.activeSelf, "Walk-ins retain a readable initial.");
            Set(owners[0], "patienceLeft", 20f);
            Invoke(first, "Update");
            Require(Get<Image>(first, "portrait").sprite == impatient, "Accepted customer portrait follows live impatience.");
            Require(first.ClockShowing, "Urgency is readable without relying on face or colour: the clock icon is up under half patience.");
            Require(Mathf.Approximately(first.RingFill, .2f), "Patience remains live (the ring).");

            var drink = AssetDatabase.LoadAssetAtPath<DrinkDefinition>("Assets/AssetsPrefabs/Drinks/Drink_HotChocolate.asset");
            Require(drink != null, "Long drink-name fixture exists.");
            Set(owners[0], "drinkOrdered", true);
            Set(owners[0], "drinkWish", drink);
            Invoke(first, "Update");
            Require(first.Items.Contains(JobTicket.DrinkIconName(drink)), $"The drink shows as its own icon beside the device ({first.Items}).");
            Invoke(first, "RefreshDetails", (object)null);
            var detail = Get<TMP_Text>(first, "detailText");
            // 20 Oct: the fault is written as a sentence now (JobTicket.SentenceCase), so its casing may differ from the record's.
            string fault = owners[0].Record.faultDescription;
            Require((detail.text.Contains(JobTicket.SentenceCase(fault)) || detail.text.IndexOf(fault, StringComparison.OrdinalIgnoreCase) >= 0)
                    && detail.text.Contains("+ Hot Chocolate"),
                $"Full task and constraint text remains available without abbreviation (\"{detail.text}\").");
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
            CheckFits(Get<TMP_Text>(first, "callSeconds"), "Call countdown fits.");
            Require(!LocalRect(Get<RectTransform>(first, "callLine")).Overlaps(LocalRect(Get<RectTransform>(first, "ring"))),
                "Support countdown does not cover the face and its ring.");
            Set(owners[0], "activeJob", null);
            Invoke(first, "Update");
            Require(!Get<RectTransform>(first, "callLine").gameObject.activeSelf
                && Get<TMP_Text>(first, "jobText").text == "" && first.Items.Contains(JobTicket.DrinkIconName(drink)) && !first.Items.Contains("item_camera"),
                "Returning the phone clears its obligation and keeps the remaining drink.");

            // Three tickets: wide dockets, with the sentence.
            for (int i = tickets.Count - 1; i >= 3; i--) { UnityEngine.Object.DestroyImmediate(tickets[i].gameObject); tickets.RemoveAt(i); }
            Invoke(manager, "LateUpdate");
            CheckRail(tickets, railRect, 1920, JobTicket.WideWidth, true);
            Require(manager.FormNow == JobTicket.Form.Wide, "Three tickets are wide dockets.");
            Require(Get<TMP_Text>(tickets[1], "jobText").gameObject.activeSelf && Get<TMP_Text>(tickets[1], "jobText").text.Length > 0,
                "A wide docket carries the fault as a sentence.");
            CheckFits(Get<TMP_Text>(tickets[1], "slotText"), "The name fits a wide docket.");

            // A narrow viewport: still one row; the dockets close up and overlap, the newest on top.
            for (int i = 0; i < 3; i++)
            {
                var ticket = UnityEngine.Object.Instantiate(prefab, railRect);
                ticket.Bind(owners[3 + i]);
                tickets.Add(ticket);
            }
            canvasRect.sizeDelta = new Vector2(1280, 720);
            Invoke(manager, "LateUpdate");
            CheckRail(tickets, railRect, 1280, JobTicket.NarrowWidth, false);
            Debug.Log("[Ticket layout] PASS: real prefab, six narrow dockets in one row in the top left corner clear of the window, three wide dockets with their sentence, a narrow viewport closing the row up without a second row, live portrait/fallback, the patience ring, the items as icons, full constraints, support countdown and handback. No scenes or saves changed.");
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

    private static void CheckRail(List<JobTicket> tickets, RectTransform rail, float canvasWidth, float width, bool noOverlap)
    {
        var rows = new HashSet<float>();
        for (int i = 0; i < tickets.Count; i++)
        {
            var rect = (RectTransform)tickets[i].transform;
            rows.Add(rect.anchoredPosition.y);
            Require(Mathf.Approximately(rect.rect.height, TicketRailUI.TicketHeight - JobTicket.FootHeight) && Mathf.Approximately(rect.rect.width, width),
                $"Every docket is the same size at this count ({width} wide).");
            Rect bounds = LocalRect(rect);
            Require(bounds.xMin >= -.01f && bounds.xMax <= rail.rect.width + .01f, "Every ticket stays inside the available rail width.");
            if (noOverlap)
                for (int j = 0; j < i; j++) Require(!bounds.Overlaps(LocalRect((RectTransform)tickets[j].transform)), "Ticket cards do not overlap while the row fits.");
            else if (i > 0)
                Require(bounds.xMin > LocalRect((RectTransform)tickets[i - 1].transform).xMin, "Past the room, the dockets close up in order (the newest on top).");
        }
        Require(rows.Count == 1, "The rail is one row at any count (his answer, 24 Oct: never a second row).");
        Require(UiSkin.Margin + rail.rect.width <= TicketRailUI.RailEnd(canvasWidth) + .01f, "The rail ends before the window top centre on this canvas.");
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
