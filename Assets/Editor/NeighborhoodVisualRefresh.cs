#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Bounded, authored visual pass. Service anchors, collision and traffic logic are retained.
public static class NeighborhoodVisualRefresh
{
    const string Folder = "Assets/Art/CC0Neighborhood";
    const string RootName = "13 - CC0 neighborhood refresh";
    static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    static readonly Vector3[] Trees = { new Vector3(16.5f,0,5.6f), new Vector3(16.5f,0,15.5f), new Vector3(-6.6f,0,19.5f) };
    static readonly Vector3[] Benches = { new Vector3(16.5f,0,1.5f), new Vector3(16.5f,0,10.7f), new Vector3(16.5f,0,19.6f), new Vector3(-.4f,0,19.5f) };
    static Transform Find(string name) => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.gameObject.scene == SceneManager.GetActiveScene() && t.name == name);
    static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
    }
    static string ModelPath(string name) => AssetDatabase.FindAssets(name + " t:Model", new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath).First(p => System.IO.Path.GetFileNameWithoutExtension(p) == name);
    static Material Mat(string name, Color color, float metallic = 0, float smooth = .27f)
    {
        if (Materials.TryGetValue(name, out var cached)) return cached;
        var path = Folder + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
        m.name=name; m.SetColor("_BaseColor",color); m.SetFloat("_Metallic",metallic); m.SetFloat("_Smoothness",smooth);
        m.enableInstancing=true; EditorUtility.SetDirty(m); Materials[name]=m; return m;
    }
    static Material Palette(string name)
    {
        string n=name.ToLowerInvariant();
        if (n.Contains("bark")) return Mat("Warm bark",new Color(.28f,.18f,.12f));
        if (n.Contains("leaf") || n.Contains("grass")) return Mat("Sage foliage",new Color(.24f,.39f,.25f));
        if (n.Contains("red")) return Mat("Coral flowers",new Color(.77f,.29f,.22f));
        if (n.Contains("purple")) return Mat("Lavender flowers",new Color(.49f,.36f,.60f));
        if (n.Contains("yellow")) return Mat("Butter flowers",new Color(.93f,.67f,.25f));
        if (n.Contains("white")) return Mat("Cream petals",new Color(.92f,.88f,.74f));
        if (n.Contains("cushion") || n.Contains("fabric")) return Mat("Moss cushion",new Color(.32f,.40f,.28f));
        if (n.Contains("metal") || n.Contains("black")) return Mat("Graphite iron",new Color(.095f,.12f,.12f),.42f);
        return Mat("Honey timber",new Color(.46f,.29f,.16f));
    }
    static Bounds BoundsOf(Transform t)
    {
        var rs=t.GetComponentsInChildren<Renderer>(); if(rs.Length==0) throw new InvalidOperationException("No model renderers: "+t.name);
        var b=rs[0].bounds; foreach(var r in rs.Skip(1)) b.Encapsulate(r.bounds); return b;
    }
    // Fits while upright at the origin, then places the finished model. Sources keep their import orientation.
    static Transform Model(string name, Transform parent, Vector3 foot, float yaw, Vector3 size)
    {
        var pivot=Group(name,parent); pivot.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(name));
        var t=((GameObject)PrefabUtility.InstantiatePrefab(source)).transform; t.SetParent(pivot,false);
        foreach(var c in t.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        foreach(var r in t.GetComponentsInChildren<Renderer>()) r.sharedMaterials=r.sharedMaterials.Select(m=>Palette(m ? m.name : "wood")).ToArray();
        var b=BoundsOf(t); pivot.localScale=Vector3.Scale(pivot.localScale,new Vector3(size.x/b.size.x,size.y/b.size.y,size.z/b.size.z));
        b=BoundsOf(t); t.position-=new Vector3(b.center.x,b.min.y,b.center.z);
        pivot.SetPositionAndRotation(foot,Quaternion.Euler(0,yaw,0)); return pivot;
    }
    // Re-running the pass (after reopening the scene without saving) rewrites the
    // same mesh asset in place instead of leaving "retained 1", "retained 2"...
    static Mesh SaveMesh(Mesh mesh,string path)
    {
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!existing) { AssetDatabase.CreateAsset(mesh,path); return mesh; }
        EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing);
        return existing;
    }
    static void HideRenderers(Transform t)
    {
        foreach(var r in t.GetComponentsInChildren<Renderer>(true)) { Undo.RecordObject(r,"Replace scenery visual"); r.enabled=false; }
    }
    public static string Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!=AcesCafeLayoutSetup.ScenePath) throw new InvalidOperationException("Open the stopped cafe layout.");
        if(Find(RootName)) throw new InvalidOperationException("Visual refresh already exists; do not duplicate it.");
        Preflight();
        // One undo step (Ctrl+Z) takes the whole pass back out of the scene.
        Undo.IncrementCurrentGroup(); int undoGroup=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Cafe neighborhood visual refresh");
        System.IO.Directory.CreateDirectory(Folder+"/Materials"); System.IO.Directory.CreateDirectory(Folder+"/Meshes"); AssetDatabase.Refresh();
        Materials.Clear();
        var root=Group(RootName,Find("ACE'S CAFE - layout study 02"));
        Undo.RegisterCreatedObjectUndo(root.gameObject,"Cafe neighborhood visual refresh");
        root.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild=true;
        Seating(root); Courtyard(root); Gardens(root); Entrance(root); Signals(root);
        Undo.CollapseUndoOperations(undoGroup);
        Physics.SyncTransforms(); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
        return "Authored seating, courtyard trees, shrubs, flowers and rounded door pulls. Existing collision and service anchors retained.\nCourtyard cut (triangles removed per combined mesh): "+CourtyardReport;
    }
    // Everything the pass edits or places must exist before anything changes,
    // so a renamed object can never leave the café half-refreshed.
    static void Preflight()
    {
        var missing=new List<string>();
        foreach(var name in new[]{"ACE'S CAFE - layout study 02","05 - seating - four neighborhood tables","08 - front patio bistro nook",
            "V3 - courtyard and corner details","OpenEntrance","V4 - junction signal housings","V4 - through traffic"})
            if(!Find(name)) missing.Add("scene object '"+name+"'");
        foreach(var model in new[]{"chairRounded","tree_oak","tree_small","bench","plant_bushDetailed","plant_bushLarge","flower_redC","flower_yellowA","flower_purpleC","SignalHousing"})
            if(!AssetDatabase.FindAssets(model+" t:Model",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath).Any(p=>System.IO.Path.GetFileNameWithoutExtension(p)==model)) missing.Add("model '"+model+"'");
        if(!AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Authored/EntranceRefresh.fbx")) missing.Add("model 'Authored/EntranceRefresh.fbx'");
        if(missing.Count>0) throw new InvalidOperationException("Nothing was changed. Missing: "+string.Join(", ",missing));
    }
    static void Seating(Transform root)
    {
        var group=Group("Warm timber cafe chairs",root);
        var tables=Find("05 - seating - four neighborhood tables");
        foreach(var table in tables.Cast<Transform>().Where(t=>t.name.StartsWith("Table ")))
        {
            var top=table.Find("Round table");
            foreach(var seat in table.Cast<Transform>().Where(t=>t.name.StartsWith("Seat ")))
            {
                var p=BoundsOf(seat).center; p.y=0;
                var direction=top.GetComponent<Renderer>().bounds.center-p; direction.y=0;
                HideRenderers(seat);
                var chair=Model("chairRounded",group,p,Quaternion.LookRotation(direction).eulerAngles.y+180,new Vector3(.45f,.93f,.45f));
                chair.name=table.name+" - "+seat.name+" timber chair";
            }
        }
        foreach(var old in Find("08 - front patio bistro nook").Cast<Transform>().Where(t=>t.name=="Outdoor chair"))
        {
            HideRenderers(old); Model("chairRounded",group,old.position,old.eulerAngles.y+180,new Vector3(.51f,.93f,.49f)).name="Patio rounded timber chair";
        }
    }
    // Which old courtyard piece a triangle belonged to (-1: keep it). Geometry
    // was combined by material by the earlier street pass (CafeStreetUpgrade
    // V3Courtyard), so only complete triangles inside known furniture bounds go.
    static int ReplacedCourtyardPiece(Vector3 a,Vector3 b,Vector3 c)
    {
        for(int i=0;i<Trees.Length;i++)
        {
            // Old canopy spheres sit above 1.6 m and the old trunk is a 12 cm
            // cylinder from 0.55 to 2.13 m. The terracotta box (top 0.58 m) and
            // its soil (top 0.60 m) stay: the first cut started at 0.56 m, took
            // both, and left the planters hollow under the new trees.
            var canopy=new Bounds(Trees[i]+Vector3.up*2.3f,new Vector3(2.1f,2.6f,2.1f));
            var trunk=new Bounds(Trees[i]+Vector3.up*1.34f,new Vector3(.2f,1.62f,.2f));
            if(Inside(canopy,a,b,c)||Inside(trunk,a,b,c)) return i;
        }
        for(int i=0;i<Benches.Length;i++)
        {
            var region=new Bounds(Benches[i]+Vector3.up*.51f,i<3 ? new Vector3(.8f,1.05f,1.8f) : new Vector3(1.8f,1.05f,.8f));
            if(Inside(region,a,b,c)) return Trees.Length+i;
        }
        return -1;
    }
    static bool Inside(Bounds region,Vector3 a,Vector3 b,Vector3 c)=>region.Contains(a)&&region.Contains(b)&&region.Contains(c);
    // What the courtyard cut removed, per combined mesh and per old piece, for the log.
    public static string CourtyardReport { get; private set; } = "";
    static void Courtyard(Transform root)
    {
        var old=Find("V3 - courtyard and corner details");
        var report=new List<string>();
        foreach(var filter in old.GetComponentsInChildren<MeshFilter>())
        {
            var original=filter.sharedMesh; var verts=original.vertices; var mesh=Object.Instantiate(original);
            var removed=new int[Trees.Length+Benches.Length]; int total=0;
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                var triangles=mesh.GetTriangles(sub); var keep=new List<int>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int piece=ReplacedCourtyardPiece(filter.transform.TransformPoint(verts[triangles[i]]),filter.transform.TransformPoint(verts[triangles[i+1]]),filter.transform.TransformPoint(verts[triangles[i+2]]));
                    if(piece>=0) { removed[piece]++; total++; continue; }
                    keep.Add(triangles[i]); keep.Add(triangles[i+1]); keep.Add(triangles[i+2]);
                }
                mesh.SetTriangles(keep,sub);
            }
            if(total==0) { Object.DestroyImmediate(mesh); continue; }
            report.Add(filter.name+" -"+total+" ("+string.Join(" ",removed.Select((n,i)=>n==0?null:(i<Trees.Length?"tree"+(i+1):"bench"+(i-Trees.Length+1))+":"+n).Where(s=>s!=null))+")");
            // Named like its file, or Unity warns "Main Object Name ... does not match filename".
            mesh.name=filter.name+" retained"; mesh.RecalculateBounds();
            Undo.RecordObject(filter,"Replace combined courtyard furniture"); filter.sharedMesh=SaveMesh(mesh,Folder+"/Meshes/"+filter.name+" retained.asset");
        }
        CourtyardReport=string.Join("; ",report);
        var group=Group("Courtyard trees and benches",root);
        for(int i=0;i<Trees.Length;i++) Model(i==1?"tree_small":"tree_oak",group,Trees[i]+Vector3.up*.60f,i*71,new Vector3(1.85f,2.9f,1.8f));
        for(int i=0;i<Benches.Length;i++) Model("bench",group,Benches[i],i<3?90:0,new Vector3(1.65f,.91f,.54f));
    }
    static void Gardens(Transform root)
    {
        var g=Group("Sidewalk pocket gardens",root);
        var centers=new[]{new Vector3(1,.135f,-14),new Vector3(8,.135f,-14),new Vector3(17.2f,.135f,7.5f),new Vector3(3,.135f,28.3f)};
        for(int p=0;p<centers.Length;p++)
        {
            for(int i=0;i<3;i++) Model("plant_bushDetailed",g,centers[p]+new Vector3(-.85f+i*.85f,0,.15f),31+i*71+p*17,new Vector3(.73f,.50f,.58f));
            for(int i=0;i<7;i++) Model(i%3==0?"flower_redC":i%3==1?"flower_yellowA":"flower_purpleC",g,centers[p]+new Vector3(-1.15f+i*.38f,0,-.29f),i*47,new Vector3(.19f,.24f+(i%2)*.08f,.19f));
        }
        // Low planting stays inside the existing terracotta tree boxes, clear of the promenade.
        foreach(var p in Trees)
        {
            Model("plant_bushLarge",g,p+new Vector3(.22f,.61f,.2f),44,new Vector3(.39f,.34f,.38f));
            Model("flower_yellowA",g,p+new Vector3(-.23f,.61f,-.23f),76,new Vector3(.22f,.29f,.23f));
        }
    }
    static void Entrance(Transform root)
    {
        var original=Find("OpenEntrance");
        var existing=original.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m).GroupBy(m=>m.name).ToDictionary(g=>g.Key,g=>g.First());
        var t=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Authored/EntranceRefresh.fbx"))).transform;
        t.SetParent(original.parent,false); t.name="Entrance with rounded brass pulls";
        foreach(var r in t.GetComponentsInChildren<Renderer>())
        {
            r.sharedMaterials=r.sharedMaterials.Select(m=>existing.TryGetValue(m.name,out var replacement)?replacement:m).ToArray();
            if(r.sharedMaterials.Any(m=>m.name=="InteriorGlass")) { r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows=false; }
        }
        Undo.RegisterCreatedObjectUndo(t.gameObject,"Rounded entrance pulls"); HideRenderers(original);
    }
    static void Signals(Transform root)
    {
        HideRenderers(Find("V4 - junction signal housings"));
        var g=Group("Hooded junction signals",root);
        var steel=Mat("Graphite iron",new Color(.095f,.12f,.12f),.42f);
        var ochre=Mat("Signal ochre",new Color(.79f,.48f,.12f),.18f,.32f);
        foreach(var signal in Find("V4 - through traffic").Cast<Transform>().Where(t=>t.name=="Traffic signal"))
        {
            var pole=Group("Signal post",g); pole.SetPositionAndRotation(signal.position,signal.rotation);
            foreach(var part in new[]{new Vector3(.072f,1.32f,.072f),new Vector3(.16f,.055f,.16f)})
            {
                var c=GameObject.CreatePrimitive(PrimitiveType.Cylinder); c.name=part.y>1?"Slim steel post":"Cast mounting foot";
                Object.DestroyImmediate(c.GetComponent<Collider>()); c.transform.SetParent(pole,false); c.transform.localPosition=Vector3.up*part.y; c.transform.localScale=part;
                c.GetComponent<Renderer>().sharedMaterial=steel;
            }
            var housing=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath("SignalHousing")))).transform;
            housing.SetParent(pole,false); housing.localPosition=new Vector3(0,2.37f,0);
            foreach(var r in housing.GetComponentsInChildren<Renderer>()) r.sharedMaterials=r.sharedMaterials.Select(m=>m.name.Contains("Ochre")?ochre:steel).ToArray();
        }
    }
    public static void Capture(string path,Vector3 position,Vector3 target)
    {
        var g=new GameObject("Temporary scenery review camera"); var cam=g.AddComponent<Camera>(); cam.enabled=false;
        var rt=new RenderTexture(1440,900,24); var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            cam.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position)); cam.fieldOfView=62; cam.nearClipPlane=.05f; cam.farClipPlane=180;
            cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt; texture.ReadPixels(new Rect(0,0,1440,900),0,0); texture.Apply();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)); System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());
        }
        finally { RenderTexture.active=previous; cam.targetTexture=null; Object.DestroyImmediate(g); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture); }
    }
}
#endif
