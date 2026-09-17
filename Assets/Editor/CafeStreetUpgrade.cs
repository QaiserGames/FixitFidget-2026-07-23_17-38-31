using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// One editor pass; the result remains an ordinary editable scene.
public static class CafeStreetUpgrade
{
    const string Folder = "Assets/Playtests/AcesCafeLayout";
    const string Marker = "07 - San Francisco neighborhood";
    static readonly Dictionary<string, Material> mats = new();
    static bool hasGeometry;
    static Transform street;

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().path != AcesCafeLayoutSetup.ScenePath)
            throw new InvalidOperationException("Open the stopped cafe layout.");
        if (FindOptional(Marker)) return "Street upgrade already exists; edit its scene objects directly.";
        if (!FindOptional("Circulation v2 - room 14.8 x 18"))
            throw new InvalidOperationException("Apply the circulation expansion first.");
        if (AssetDatabase.LoadMainAssetAtPath(Folder + "/Street geometry.asset"))
            throw new InvalidOperationException("Street assets already exist; review the prior pass before rebuilding.");
        hasGeometry = false;
        ConfigureImports();
        MakeMaterials();
        var root = Find("ACE'S CAFE - layout study 02");
        street = Group(Marker, root);
        street.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        Roads();
        string[] colors = { "Saffron", "Dusty rose", "Sea green", "Lavender", "Sky blue", "Saffron" };
        for (int i = 0; i < colors.Length; i++) House(i, colors[i], -4f + i * 5.8f, i % 3 == 0 ? 8.2f : 10.5f);
        StreetFurniture();
        CounterWings();
        Patio(root);
        MoveDecor();
        AddLife();
        Object.DestroyImmediate(Find("07 - street context - visual only").gameObject);
        FrameCamera();
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        return "Neighborhood applied: 6 bay-window buildings, matching counter wings, patio bistro table, 2 cars, 3 pedestrians, a cat and 2 birds. Review, bake routes and save.";
    }

    static void ConfigureImports()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder + "/Textures" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = path.Contains("normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !path.Contains("normal");
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }
        foreach (string name in new[] { "Cat", "Eagle" })
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "/StreetModels/" + name + ".fbx");
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips) { clip.loopTime = true; clip.lockRootPositionXZ = true; clip.lockRootHeightY = true; }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    static Material Mat(string name, Color color, string texture = null, float tiling = 1f)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", .18f);
        if (texture != null)
        {
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + texture + "_color.jpg"));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + texture + "_normal.jpg"));
            m.SetFloat("_BumpScale", .28f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTextureScale("_BaseMap", Vector2.one * tiling);
        }
        if (mats.Count == 0) AssetDatabase.CreateAsset(m, Folder + "/Street palette.asset");
        else AssetDatabase.AddObjectToAsset(m, Folder + "/Street palette.asset");
        mats.Add(name, m);
        return m;
    }

    static void MakeMaterials()
    {
        mats.Clear();
        Mat("Saffron", new Color(.88f,.68f,.32f), "painted_plaster_wall", .5f);
        Mat("Dusty rose", new Color(.79f,.40f,.38f), "painted_plaster_wall", .5f);
        Mat("Sea green", new Color(.40f,.64f,.56f), "painted_plaster_wall", .5f);
        Mat("Lavender", new Color(.58f,.53f,.73f), "painted_plaster_wall", .5f);
        Mat("Sky blue", new Color(.40f,.60f,.74f), "painted_plaster_wall", .5f);
        Mat("Cream trim", new Color(.96f,.87f,.69f));
        Mat("Dark joinery", new Color(.14f,.25f,.25f));
        Mat("Window glass", new Color(.14f,.29f,.37f)).SetFloat("_Smoothness", .72f);
        Mat("Curtain glow", new Color(.96f,.70f,.35f));
        Mat("Slate roof", new Color(.18f,.22f,.24f));
        Mat("Ironwork", new Color(.09f,.13f,.13f));
        Mat("Sidewalk stone", new Color(.70f,.68f,.60f), "painted_plaster_wall", .5f);
        Mat("Asphalt", new Color(.69f,.72f,.74f), "asphalt_floor", .34f);
        Mat("Road paint", new Color(.96f,.77f,.36f));
        Mat("Planter clay", new Color(.65f,.34f,.24f));
        Mat("Leaf green", new Color(.33f,.47f,.30f));
        var lamp = Mat("Lamp glass", new Color(1,.84f,.54f));
        lamp.EnableKeyword("_EMISSION"); lamp.SetColor("_EmissionColor", new Color(1,.67f,.28f) * .65f);
        var car = Mat("Car atlas", Color.white);
        car.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/car-colormap.png"));
        car.SetFloat("_Smoothness", .4f);
        Mat("Cat cream", new Color(.83f,.77f,.63f));
        Mat("Cat charcoal", new Color(.20f,.22f,.24f));
        Mat("Cat pink", new Color(.84f,.48f,.48f));
        Mat("Bird feathers", new Color(.30f,.36f,.40f));
    }

    static Transform Find(string name) => FindOptional(name) ?? throw new InvalidOperationException("Missing: " + name);
    static Transform FindOptional(string name) => SceneManager.GetActiveScene().GetRootGameObjects()
        .SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name);
    static Transform Group(string name, Transform parent)
    { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
    static GameObject Part(Transform parent, string name, Vector3 p, Vector3 s, string mat, PrimitiveType type = PrimitiveType.Cube)
    {
        var g = GameObject.CreatePrimitive(type); g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = p; g.transform.localScale = s;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.GetComponent<Renderer>().sharedMaterial = mats[mat]; return g;
    }
    static float Ground(float z) => -.48f + .075f * z;
    static GameObject Sloped(Transform p, string n, float x, float y, float z, float w, float h, float d, string mat)
    {
        var g = Part(p,n,new Vector3(x,Ground(z)+y,z),new Vector3(w,h,d),mat);
        g.transform.localRotation = Quaternion.Euler(-Mathf.Atan(.075f)*Mathf.Rad2Deg,0,0); return g;
    }
    static void Roads()
    {
        var g = Group("Street - paving and road markings",street);
        Sloped(g,"Uphill two lane road",-12.2f,-.12f,8,6,.24f,72,"Asphalt");
        Sloped(g,"Far sidewalk",-16.3f,.06f,8,2.2f,.3f,72,"Sidewalk stone");
        // The cafe sits on a level terrace; a continuous curb meets the sloping road.
        Part(g,"Cafe terrace",new Vector3(-8.2f,-.18f,8),new Vector3(1.6f,.36f,26),"Sidewalk stone");
        if (FindOptional("09 - neighborhood surrounds V3"))
        {
            // Leave the cross-street mouth open while preserving the uphill curb.
            Sloped(g,"Near curb - downhill",-9.12f,.07f,-20.8f,.18f,.34f,14.4f,"Cream trim");
            Sloped(g,"Near curb - uphill",-9.12f,.07f,18.3f,.18f,.34f,51.4f,"Cream trim");
        }
        else Sloped(g,"Near curb",-9.12f,.07f,8,.18f,.34f,72,"Cream trim");
        Sloped(g,"Far curb",-15.24f,.07f,8,.18f,.34f,72,"Cream trim");
        for(int i=0;i<22;i++)
            foreach(float x in new[]{-12.13f,-12.27f}) Sloped(g,"Double yellow",x,.008f,-25+i*3.2f,.045f,.012f,2.3f,"Road paint");
        for(int i=0;i<7;i++) Sloped(g,"Crosswalk",-14.7f+i*.83f,.016f,-8.0f,.40f,.015f,2.1f,"Cream trim");
        for(int i=0;i<31;i++)
        {
            float z=-27+i*2.3f;
            Sloped(g,"Sidewalk joint",-16.3f,.217f,z,2.15f,.008f,.018f,"Slate roof");
        }
        for(int i=0;i<11;i++) Part(g,"Terrace joint",new Vector3(-8.2f,.005f,-4+i*2.3f),new Vector3(1.5f,.008f,.015f),"Slate roof");
        Combine(g);
    }

    static void House(int index,string color,float z,float height)
    {
        var g=Group((index+1)+" - "+color+" bay-window house",street);
        g.SetPositionAndRotation(new Vector3(-17.55f,Ground(z)+.22f,z),Quaternion.Euler(0,90,0));
        Part(g,"Painted house",new Vector3(0,height/2,-2.1f),new Vector3(5.6f,height,4.2f),color);
        Part(g,"Stone foundation",new Vector3(0,.27f,-2.03f),new Vector3(5.66f,.54f,4.25f),"Sidewalk stone");
        // Thin projecting courses give painted timber real depth, including side walls.
        for(float y=.8f;y<height-.25f;y+=.25f)
            Part(g,"Clapboard lip",new Vector3(0,y,.025f),new Vector3(5.6f,.035f,.05f),color);
        foreach(float x in new[]{-2.73f,2.73f})
            Part(g,"Corner trim",new Vector3(x,height/2,.055f),new Vector3(.14f,height,.14f),"Cream trim");
        for(float y=2.55f;y+2.05f<height;y+=2.6f)
        {
            Part(g,"Floor stringcourse",new Vector3(0,y,.08f),new Vector3(5.78f,.12f,.20f),"Cream trim");
            foreach(float x in new[]{-1.35f,1.35f})
            {
                var bay=Group("Projecting bay",g); bay.localPosition=new Vector3(x,y+.90f,0);
                Part(bay,"Bay cabinet",new Vector3(0,0,.4f),new Vector3(1.5f,2.3f,.8f),color);
                Window(bay,new Vector3(0,.02f,.825f),0,1.34f,1.65f,index%3==0);
                Window(bay,new Vector3(-.64f,.02f,.58f),-45,.57f,1.65f,false);
                Window(bay,new Vector3(.64f,.02f,.58f),45,.57f,1.65f,false);
                Part(bay,"Bay sill",new Vector3(0,-.91f,.48f),new Vector3(1.77f,.14f,1.02f),"Cream trim");
                Part(bay,"Bay crown",new Vector3(0,1.08f,.46f),new Vector3(1.85f,.18f,1.03f),"Cream trim");
                foreach(float xx in new[]{-.57f,.57f})
                {
                    var bracket=Part(bay,"Carved support",new Vector3(xx,-1.04f,.37f),new Vector3(.13f,.38f,.4f),"Cream trim");
                    bracket.transform.localRotation=Quaternion.Euler(20,0,0);
                }
            }
        }
        Part(g,"Roof slate",new Vector3(0,height+.08f,-2.02f),new Vector3(5.87f,.18f,4.48f),"Slate roof");
        Part(g,"Overhanging cornice",new Vector3(0,height,.12f),new Vector3(6,.28f,.48f),"Cream trim");
        for(int i=0;i<17;i++) Part(g,"Cornice bracket",new Vector3(-2.65f+i*.33f,height-.27f,.18f),new Vector3(.13f,.30f,.28f),"Cream trim");
        Part(g,"Chimney",new Vector3(-1.65f,height+.54f,-2.7f),new Vector3(.52f,1.05f,.7f),"Planter clay");
        Part(g,"Chimney cap",new Vector3(-1.65f,height+1.08f,-2.7f),new Vector3(.65f,.14f,.85f),"Cream trim");
        Part(g,"Front door frame",new Vector3(1.6f,1.31f,.10f),new Vector3(1.22f,2.48f,.20f),"Cream trim");
        Part(g,"Front door",new Vector3(1.6f,1.24f,.23f),new Vector3(.98f,2.2f,.12f),"Dark joinery");
        Part(g,"Door glass",new Vector3(1.6f,1.68f,.30f),new Vector3(.68f,.82f,.016f),"Window glass");
        Part(g,"Door panel",new Vector3(1.6f,.60f,.31f),new Vector3(.70f,.60f,.03f),color);
        Part(g,"Brass door knob",new Vector3(1.92f,1.04f,.34f),Vector3.one*.075f,"Road paint",PrimitiveType.Sphere);
        Window(g,new Vector3(-1.05f,1.48f,.09f),0,2.25f,1.45f,index%2==0);
        for(int i=0;i<3;i++) Part(g,"Stoop step",new Vector3(1.6f,.08f-i*.065f,.42f+i*.22f),new Vector3(1.55f,.14f,.36f),"Sidewalk stone");
        foreach(float x in new[]{.78f,2.42f})
        {
            for(int i=0;i<4;i++) Part(g,"Stoop baluster",new Vector3(x,.45f,.22f+i*.24f),new Vector3(.035f,.9f,.035f),"Ironwork");
            Part(g,"Stoop handrail",new Vector3(x,.91f,.59f),new Vector3(.065f,.06f,1.04f),"Ironwork");
        }
        if(index==1 || index==4)
        {
            for(int i=0;i<12;i++)
            {
                var awning=Part(g,"Striped shop awning",new Vector3(-1.05f+i*.23f-1.27f,2.37f,.62f),new Vector3(.23f,.085f,1.13f),i%2==0?"Cream trim":"Dark joinery");
                awning.transform.localRotation=Quaternion.Euler(13,0,0);
                Part(g,"Awning valance",new Vector3(-2.32f+i*.23f,2.15f,1.16f),new Vector3(.23f,.23f,.06f),i%2==0?"Cream trim":"Dark joinery");
            }
        }
        Combine(g);
    }

    static void Window(Transform p,Vector3 pos,float yaw,float width,float height,bool warm)
    {
        var g=Group("Framed sash",p);g.localPosition=pos;g.localRotation=Quaternion.Euler(0,yaw,0);
        Part(g,"Recessed glazing",Vector3.zero,new Vector3(width,height,.05f),"Window glass");
        if(warm) foreach(float x in new[]{-.32f,.32f}) Part(g,"Curtain",new Vector3(width*x,0,.03f),new Vector3(width*.22f,height-.1f,.018f),"Curtain glow");
        foreach(float x in new[]{-width/2,width/2}) Part(g,"Side casing",new Vector3(x,0,.045f),new Vector3(.09f,height+.15f,.10f),"Cream trim");
        foreach(float y in new[]{-height/2,0,height/2}) Part(g,"Sash rail",new Vector3(0,y,.045f),new Vector3(width+.12f,.065f,.10f),"Cream trim");
        Part(g,"Central mullion",new Vector3(0,0,.045f),new Vector3(.045f,height,.10f),"Cream trim");
    }

    static void StreetFurniture(bool levelBlock = false)
    {
        var g=Group("Street furniture",street);
        foreach(float z in new[]{-9f,4f,17f,30f})
        {
            var lamp=Group("Neighborhood street lamp",g);lamp.position=new Vector3(levelBlock ? -17.1f : -15.5f,levelBlock ? 0 : Ground(z)+.23f,z);
            Part(lamp,"Foot",new Vector3(0,.16f,0),new Vector3(.33f,.32f,.33f),"Ironwork",PrimitiveType.Cylinder);
            Part(lamp,"Pole",new Vector3(0,1.85f,0),new Vector3(.075f,1.85f,.075f),"Ironwork",PrimitiveType.Cylinder);
            Part(lamp,"Lantern",new Vector3(0,3.70f,0),new Vector3(.36f,.45f,.36f),"Lamp glass");
            Part(lamp,"Lantern cap",new Vector3(0,3.96f,0),new Vector3(.51f,.12f,.51f),"Ironwork");
            foreach(float x in new[]{-.18f,.18f}) foreach(float zz in new[]{-.18f,.18f})
                Part(lamp,"Lantern frame",new Vector3(x,3.7f,zz),new Vector3(.035f,.48f,.035f),"Ironwork");
        }
        // A few outdoor trees; the cafe interior remains focused on artwork.
        foreach(float z in new[]{-13f,33f})
        {
            var tree=Group("Sidewalk tree",g); tree.position=new Vector3(-17.05f,levelBlock ? 0 : Ground(z)+.22f,z);
            Part(tree,"Trunk",new Vector3(0,1.2f,0),new Vector3(.20f,1.2f,.20f),"Planter clay",PrimitiveType.Cylinder);
            foreach(var p in new[]{new Vector3(-.4f,2.5f,0),new Vector3(.5f,2.9f,.1f),new Vector3(0,3.5f,0)})
                Part(tree,"Canopy",p,new Vector3(1.7f,1.65f,1.7f),"Leaf green",PrimitiveType.Sphere);
        }
        Combine(g);
    }

    static Transform Model(string path,string name,Transform parent,Vector3 desiredSize,bool uniform)
    {
        var wrapper=Group(name,parent);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(!prefab) throw new InvalidOperationException("Missing model "+path);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,wrapper);
        model.transform.localPosition=Vector3.zero;
        var renderers=model.GetComponentsInChildren<Renderer>();
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        Vector3 size=bounds.size;
        float scalar=desiredSize.y/size.y;
        wrapper.localScale=uniform?Vector3.one*scalar:new Vector3(desiredSize.x/size.x,desiredSize.y/size.y,desiredSize.z/size.z);
        bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        model.transform.position += new Vector3(wrapper.position.x-bounds.center.x,wrapper.position.y-bounds.min.y,wrapper.position.z-bounds.center.z);
        foreach(var c in model.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
        return wrapper;
    }

    static Transform ActorModel(string path,string name,Transform parent,float scale)
    {
        // These scales are calibrated from the source meshes and verified in Play Mode.
        // Skinned culling bounds and uninitialized baked poses are unsuitable for sizing.
        var wrapper=Group(name,parent);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),wrapper);
        model.transform.localPosition=Vector3.zero;
        wrapper.localScale=Vector3.one*scale;
        foreach(var collider in model.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
        return wrapper;
    }

    static void CounterWings()
    {
        var group=Find("Counter extensions and cabinetry");
        foreach(var t in group.Cast<Transform>().Where(t=>t.name=="Left counter wing"||t.name=="Right counter wing"||t.name=="Wing countertop"||t.name=="Counter front batten").ToArray()) Object.DestroyImmediate(t.gameObject);
        foreach(var spec in new[]{new Vector2(-6.05f,2.7f),new Vector2(-3.35f,2.7f),new Vector2(3.26f,3.08f)})
        {
            var wing=Model("Assets/Art/Models/Counter.fbx","Matching counter section",group,new Vector3(spec.y,1.095f,.94f),false);
            wing.SetPositionAndRotation(new Vector3(spec.x,0,13.75f),Quaternion.Euler(0,180,0));
            var col=wing.gameObject.AddComponent<BoxCollider>();
            col.center=new Vector3(0,.535f/wing.localScale.y,0);
            col.size=new Vector3(spec.y/wing.localScale.x,1.07f/wing.localScale.y,1f/wing.localScale.z);
        }
    }

    static void Patio(Transform root)
    {
        var g=Group("08 - front patio bistro nook",root);
        var table=Model("Assets/Art/Models/T2_Table_BistroRound.fbx","Outdoor bistro table",g,new Vector3(.7f,.74f,.7f),true);
        table.position=new Vector3(-4.05f,0,-1.85f);
        var tc=table.gameObject.AddComponent<CapsuleCollider>();tc.radius=.36f/table.localScale.x;tc.height=.74f/table.localScale.y;tc.center=Vector3.up*.37f/table.localScale.y;
        foreach(float x in new[]{-5f,-3.1f})
        {
            var chair=Model("Assets/Art/Models/T2_Chair_Cafe.fbx","Outdoor chair",g,new Vector3(.46f,.86f,.52f),true);
            chair.SetPositionAndRotation(new Vector3(x,0,-1.85f),Quaternion.Euler(0,x<-4?90:-90,0));
            var c=chair.gameObject.AddComponent<BoxCollider>();c.center=Vector3.up*.43f/chair.localScale.y;c.size=new Vector3(.46f,.86f,.52f)/chair.localScale.x;
        }
        Part(g,"Terracotta bud vase",new Vector3(-4.05f,.82f,-1.85f),new Vector3(.10f,.075f,.10f),"Planter clay",PrimitiveType.Cylinder);
        Part(g,"Single flower",new Vector3(-4.05f,.94f,-1.85f),new Vector3(.10f,.09f,.10f),"Road paint",PrimitiveType.Sphere);
    }

    static void MoveDecor()
    {
        foreach(var t in Find("06 - atmosphere placeholders").Cast<Transform>().ToArray())
        {
            if(t.name=="Inside-only ceiling")continue;
            if(t.position.z>14)t.position+=Vector3.forward*3;
            else if(t.position.x>6)t.position+=Vector3.right;
        }
        Find("GraceReunionPhoto").position+=Vector3.forward*3;
        Find("Light_ShopWarmth").position=new Vector3(0,4.5f,12);
        Find("Light_ShopWarmth").GetComponent<Light>().range=18;
    }

    static AnimatorController Loop(string model,string clipName,string name)
    {
        var clip=AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview")&&c.name.Contains(clipName));
        string path=Folder+"/StreetModels/"+name+".controller";
        var existing=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(existing)return existing;
        var controller=AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddMotion(clip);return controller;
    }
    static void AddLife()
    {
        var g=Group("Life - decorative routes",street);
        var life=g.gameObject.AddComponent<StreetLife>();
        for(int i=0;i<2;i++)
        {
            var car=Model(Folder+"/StreetModels/"+(i==0?"sedan":"hatchback-sports")+".fbx",i==0?"Neighborhood sedan":"Neighborhood hatchback",g,new Vector3(0,1.42f,0),true);
            foreach(var r in car.GetComponentsInChildren<Renderer>())r.sharedMaterial=mats["Car atlas"];
            var route=AddActor(life,car,g,5.2f+i,.13f+i*.46f,false,true,
                RoadPoint(-10.65f,-27),RoadPoint(-10.65f,41),RoadPoint(-13.72f,44),RoadPoint(-13.72f,-30));
            route.wheels=car.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("wheel-")).ToArray();
            route.wheelRadius=.37f; route.turnSpeed=110;
        }
        for(int i=0;i<3;i++)
        {
            var person=ActorModel("Assets/ThirdParty/Quaternius_ModularMen/Beach.fbx","Walking neighbor "+(i+1),g,.92f);
            var animator=person.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/CustomerAnimator(.controller");
            animator.applyRootMotion=false;
            var actor=AddActor(life,person,g,.90f+i*.12f,.17f+i*.30f,false,false,
                WalkPoint(-15.55f,-20),WalkPoint(-15.55f,36),WalkPoint(-16.10f,37),WalkPoint(-16.10f,-21));
            actor.animator=animator;
        }
        string catPath=Folder+"/StreetModels/Cat.fbx";
        var cat=ActorModel(catPath,"Neighborhood cat",g,.125f);
        var catR=cat.GetComponentInChildren<Renderer>();catR.sharedMaterials=new[]{mats["Cat cream"],mats["Cat charcoal"],mats["Cat pink"]};
        var catA=cat.GetComponentInChildren<Animator>()??cat.GetChild(0).gameObject.AddComponent<Animator>();catA.runtimeAnimatorController=Loop(catPath,"Walking","Cat walk");
        FaceModel(cat,-90);
        var catActor=AddActor(life,cat,g,.42f,.12f,false,false,new Vector3(-6.6f,.015f,-3.35f),new Vector3(5.7f,.015f,-3.35f),new Vector3(6.2f,.015f,-3.05f),new Vector3(-6.6f,.015f,-3.05f));
        catActor.animator=catA;
        string birdPath=Folder+"/StreetModels/Eagle.fbx";
        var birdController=Loop(birdPath,"Flying","Bird flight");
        for(int i=0;i<2;i++)
        {
            var bird=ActorModel(birdPath,"Passing bird "+(i+1),g,.095f);
            bird.GetComponentInChildren<Renderer>().sharedMaterials=new[]{mats["Bird feathers"],mats["Cream trim"],mats["Road paint"],mats["Ironwork"]};
            var anim=bird.GetComponentInChildren<Animator>()??bird.GetChild(0).gameObject.AddComponent<Animator>();anim.runtimeAnimatorController=birdController;
            FaceModel(bird,180);
            var a=AddActor(life,bird,g,3.4f,.1f+i*.43f,true,false,new Vector3(-10,7,-6),new Vector3(-10.5f,9,14),new Vector3(-13.8f,10,26),new Vector3(-14,8,8));
            a.animator=anim;a.bankAngle=18;
        }
        life.RebuildRoutes(); // Also authors the initial positions for Edit Mode preview.
    }
    static Vector3 RoadPoint(float x,float z)=>new Vector3(x,Ground(z)+.01f,z);
    static void FaceModel(Transform wrapper,float yaw)
    {
        var model=wrapper.GetChild(0);var facing=Group("Model facing",wrapper);
        model.SetParent(facing,true);facing.localRotation=Quaternion.Euler(0,yaw,0);
    }
    static Vector3 WalkPoint(float x,float z)=>new Vector3(x,Ground(z)+.22f,z);
    static StreetLife.Actor AddActor(StreetLife life,Transform actor,Transform parent,float speed,float phase,bool smooth,bool slope,params Vector3[] points)
    {
        var route=Group(actor.name+" route",parent);
        var markers=points.Select((p,i)=>{var t=Group("Point "+(i+1),route);t.position=p;return t;}).ToArray();
        var a=new StreetLife.Actor{actor=actor,waypoints=markers,speed=speed,startPhase=phase,smoothRoute=smooth,alignToSlope=slope};
        life.actors.Add(a);return a;
    }

    static void FrameCamera()
    {
        var cam=Find("CmShopCam").GetComponent<CinemachineCamera>();
        cam.transform.rotation=Quaternion.Euler(50,45,0);
        cam.transform.position=new Vector3(-.65f,.4f,8.7f)-cam.transform.forward*34f;
        var lens=cam.Lens;lens.FieldOfView=41;cam.Lens=lens;
        var main=Find("Main Camera").GetComponent<Camera>();main.transform.SetPositionAndRotation(cam.transform.position,cam.transform.rotation);main.fieldOfView=lens.FieldOfView;
        var sv=SceneView.lastActiveSceneView;
        if(sv){sv.drawGizmos=false;sv.orthographic=true;sv.LookAtDirect(new Vector3(-4,2,9),Quaternion.Euler(45,45,0),19);sv.Repaint();}
    }

    // V3 extends the saved neighborhood. It never rebuilds the cafe, changes a
    // gameplay collider, or replaces the owner's ceiling and furnishing edits.
    public static string ApplyV3Geometry()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != AcesCafeLayoutSetup.ScenePath)
            throw new InvalidOperationException("Open the stopped cafe layout before applying V3.");
        if (scene.isDirty) throw new InvalidOperationException("Save the current scene edits before applying V3.");
        if (FindOptional("09 - neighborhood surrounds V3")) return "V3 surroundings already exist; edit their scene objects directly.";
        var palette = AssetDatabase.LoadAllAssetsAtPath(Folder + "/Street palette.asset").OfType<Material>().ToArray();
        if (palette.Length < 15 || !AssetDatabase.LoadMainAssetAtPath(Folder + "/Street geometry.asset"))
            throw new InvalidOperationException("The saved V2 street palette and geometry are required.");
        var room = Find("ACE'S CAFE - layout study 02");
        var oldStreet = Find(Marker);
        var rightWall = Find("Right plaster");
        var life = oldStreet.GetComponentInChildren<StreetLife>();
        if (!life) throw new InvalidOperationException("The existing decorative StreetLife controller is missing.");
        if (!rightWall.GetComponent<Renderer>() || !rightWall.GetComponent<Collider>())
            throw new InvalidOperationException("The original right wall renderer and collision boundary are required.");
        if (!AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Quaternius_ModularMen/Beach.fbx") ||
            !AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/CustomerAnimator(.controller"))
            throw new InvalidOperationException("The existing neighborhood character and walk controller are required.");
        mats.Clear();
        foreach (var material in palette) mats[material.name] = material;
        // Some Unity versions rename the main subasset to the containing file.
        if (!mats.ContainsKey("Saffron")) mats["Saffron"] = palette.First(m => m.name == "Street palette");
        hasGeometry = true;
        V3Material("Warm brick", new Color(.55f,.30f,.25f), "painted_plaster_wall");
        V3Material("Cloud plaster", new Color(.80f,.83f,.76f), "painted_plaster_wall");
        V3Material("Paper cream", new Color(.96f,.92f,.82f));
        V3Material("Distant neighborhood", new Color(.48f,.58f,.60f));
        V3Material("Courtyard paving", new Color(.65f,.63f,.52f), "painted_plaster_wall");
        V3Material("Book vermilion", new Color(.80f,.32f,.22f));
        V3Material("Coffee brown", new Color(.28f,.15f,.095f));
        V3Material("Mural indigo", new Color(.22f,.34f,.47f));
        V3Material("Mural apricot", new Color(.95f,.63f,.39f));
        var windows = mats["Curtain glow"];
        windows.EnableKeyword("_EMISSION");
        windows.SetColor("_EmissionColor", new Color(1f,.55f,.22f) * .12f);
        EditorUtility.SetDirty(windows);
        street = Group("09 - neighborhood surrounds V3", room);
        street.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        V3Ground();
        V3NeighborhoodBuildings();
        V3Courtyard();
        V3CafeWindows(rightWall);
        V3InteriorDetails();
        V3Lighting();
        V3Neighbors(life);
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        return "V3 surrounds added: front corner shops and cross-street, right courtyard and cafe windows, rear neighborhood, perimeter details, warm lights, and walking neighbors on all sides. Gameplay colliders and the ceiling were preserved. Review and save.";
    }

    static Material V3Material(string name, Color color, string texture = null)
        => mats.TryGetValue(name, out var material) ? material : Mat(name, color, texture, .6f);

    // Refresh these two decorative ground groups only. Buildings, lighting,
    // actors, playable collision and the owner's room edits stay in place.
    public static string RefreshV3GroundGeometry()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != AcesCafeLayoutSetup.ScenePath)
            throw new InvalidOperationException("Open the stopped cafe layout before refreshing its V3 ground.");
        var oldStreet = Find(Marker);
        var surrounds = Find("09 - neighborhood surrounds V3");
        var oldRoads = oldStreet.Find("Street - paving and road markings");
        var oldGround = surrounds.Find("V3 - connected neighborhood ground");
        if (!oldRoads || !oldGround)
            throw new InvalidOperationException("Both existing V2 road and V3 ground groups are required.");
        string geometryPath = Folder + "/Street geometry.asset";
        var palette = AssetDatabase.LoadAllAssetsAtPath(Folder + "/Street palette.asset").OfType<Material>().ToArray();
        if (!AssetDatabase.LoadMainAssetAtPath(geometryPath))
            throw new InvalidOperationException("The existing street geometry container is required.");
        mats.Clear();
        foreach (var material in palette) mats[material.name] = material;
        foreach (string name in new[] { "Asphalt", "Sidewalk stone", "Cream trim", "Road paint", "Slate roof",
            "Distant neighborhood", "Courtyard paving", "Warm brick" })
            if (!mats.ContainsKey(name)) throw new InvalidOperationException("Missing street material: " + name);
        hasGeometry = true;
        var oldMeshes = oldRoads.GetComponentsInChildren<MeshFilter>(true)
            .Concat(oldGround.GetComponentsInChildren<MeshFilter>(true))
            .Select(f => f.sharedMesh).Where(m => m != null).Distinct().ToArray();

        // Finish both replacements before removing the old groups.
        street = surrounds;
        V3Ground();
        street = oldStreet;
        Roads();
        Object.DestroyImmediate(oldGround.gameObject);
        Object.DestroyImmediate(oldRoads.gameObject);

        // A shared or main asset is never removed. Include inactive objects and
        // every loaded scene when checking for other mesh users.
        var inUse = new HashSet<Mesh>(Resources.FindObjectsOfTypeAll<MeshFilter>()
            .Where(f => f.gameObject.scene.IsValid()).Select(f => f.sharedMesh));
        inUse.UnionWith(Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>()
            .Where(r => r.gameObject.scene.IsValid()).Select(r => r.sharedMesh));
        inUse.UnionWith(Resources.FindObjectsOfTypeAll<MeshCollider>()
            .Where(c => c.gameObject.scene.IsValid()).Select(c => c.sharedMesh));
        int removed = 0;
        foreach (var mesh in oldMeshes)
            if (AssetDatabase.IsSubAsset(mesh) && !AssetDatabase.IsMainAsset(mesh)
                && AssetDatabase.GetAssetPath(mesh) == geometryPath && !inUse.Contains(mesh))
            { Object.DestroyImmediate(mesh, true); removed++; }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        return "V3 paving refreshed: coplanar overlaps removed, sloped road junction connected, near curb opened. "
            + removed + " unreferenced mesh subassets removed; other scene groups preserved. Review and save.";
    }

    static Mesh V3RoadJunction(Transform parent)
    {
        var g = Part(parent, "Graded corner junction", Vector3.zero, Vector3.one, "Asphalt");
        var filter = g.GetComponent<MeshFilter>();
        var mesh = Object.Instantiate(filter.sharedMesh);
        mesh.name = "Graded corner junction - authoring";
        var vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i];
            float x = Mathf.Lerp(-9.25f, -6.5f, v.x + .5f);
            float z = Mathf.Lerp(-13.5f, -7.5f, v.z + .5f);
            float top = Mathf.Lerp(Ground(z), -1.2675f, v.x + .5f);
            vertices[i] = new Vector3(x, v.y > 0 ? top : -3.2875f, z);
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;
        return mesh;
    }

    static void V3Ground()
    {
        var g = Group("V3 - connected neighborhood ground", street);
        // Continuous ground underneath all streets removes the floating diorama
        // edge, while the cafe's existing player boundaries stay untouched.
        Part(g,"Neighborhood ground",new Vector3(0,-3.45f,4),new Vector3(160,.35f,160),"Distant neighborhood");
        Part(g,"Front corner road",new Vector3(16.25f,-2.2775f,-10.5f),new Vector3(45.5f,2.02f,6),"Asphalt");
        var junctionMesh = V3RoadJunction(g);
        Part(g,"Front far sidewalk",new Vector3(11,-2.15f,-15.05f),new Vector3(52,2.25f,3.1f),"Sidewalk stone");
        Part(g,"Front far curb",new Vector3(15,-1.14f,-13.55f),new Vector3(48,.32f,.18f),"Cream trim");
        Part(g,"Front near curb",new Vector3(15,-1.14f,-7.45f),new Vector3(48,.32f,.18f),"Cream trim");
        Part(g,"Cafe corner promenade",new Vector3(-.15f,-1.65f,-5.45f),new Vector3(15.7f,3.3f,2.9f),"Courtyard paving");
        Part(g,"Promenade retaining wall",new Vector3(4,-.68f,-6.86f),new Vector3(24,1.36f,.18f),"Warm brick");
        Part(g,"Promenade cap",new Vector3(4,.08f,-6.86f),new Vector3(24,.16f,.27f),"Cream trim");
        Part(g,"Courtyard terrace",new Vector3(13.1f,-1.65f,8.8f),new Vector3(10.8f,3.3f,31.6f),"Courtyard paving");
        Part(g,"Rear terrace",new Vector3(.15f,-1.65f,21.5f),new Vector3(15.1f,3.3f,6.7f),"Courtyard paving");
        Part(g,"Rear neighborhood raised street",new Vector3(4,-.91f,27.0f),new Vector3(50,4.72f,4.5f),"Asphalt");
        Part(g,"Rear neighborhood sidewalk",new Vector3(4,-.77f,29.8f),new Vector3(50,5.05f,1.65f),"Sidewalk stone");
        Part(g,"Rear retaining brickwork",new Vector3(4,.6f,24.6f),new Vector3(50,1.8f,.25f),"Warm brick");
        Part(g,"Rear retaining cap",new Vector3(4,1.54f,24.6f),new Vector3(50,.12f,.39f),"Cream trim");
        for(int i=0;i<15;i++)
        {
            float x=-7+i*3.15f;
            foreach(float z in new[]{-10.43f,-10.57f}) Part(g,"Cross street double yellow",new Vector3(x,-1.255f,z),new Vector3(2.1f,.012f,.043f),"Road paint");
        }
        for(int i=0;i<7;i++)
            Part(g,"Front pedestrian crossing",new Vector3(6,-1.244f,-13.02f+i*.82f),new Vector3(2.15f,.013f,.40f),"Cream trim");
        for(int i=0;i<19;i++)
            Part(g,"Front sidewalk joint",new Vector3(-12+i*2.6f,-1.025f,-15.05f),new Vector3(.018f,.008f,3.0f),"Slate roof");
        for(int i=0;i<13;i++)
        {
            float z=-5.5f+i*2.4f;
            Part(g,"Courtyard stone joint",new Vector3(13.1f,.006f,z),new Vector3(10.65f,.008f,.016f),"Sidewalk stone");
        }
        for(int i=0;i<9;i++)
            Part(g,"Promenade stone joint",new Vector3(-7+i*2.8f,.006f,-5.4f),new Vector3(.018f,.008f,2.8f),"Sidewalk stone");
        // Two stair flights articulate the grade change beyond the playable apron.
        foreach(float x in new[]{2f,12.6f})
            for(int i=0;i<7;i++)
                Part(g,"Corner stair tread",new Vector3(x,-.09f-i*.18f,-6.7f-i*.24f),new Vector3(2.25f,.18f,.30f),"Sidewalk stone");
        Combine(g);
        Object.DestroyImmediate(junctionMesh);
    }

    static void V3NeighborhoodBuildings()
    {
        string[] colors={"Dusty rose","Sea green","Saffron","Sky blue","Lavender"};
        for(int i=0;i<5;i++)
            V3Shop("Front - "+(i+1),new Vector3(-4f+i*6.05f,-1.02f,-16.55f),0,5.9f, i%2==0?5.8f:6.7f,colors[i],i);
        for(int i=0;i<4;i++)
            V3Shop("Courtyard - "+(i+1),new Vector3(19.1f,0,-.5f+i*6.1f),270,5.95f,5.6f+i%2*.9f,colors[(i+2)%5],i+5);
        for(int i=0;i<5;i++)
            V3Shop("Rear hill - "+(i+1),new Vector3(-10f+i*6.0f,1.78f,30.8f),180,5.85f,6.6f+i%3*.85f,colors[(i+1)%5],i+9);
        var distant=Group("V3 - distant hill neighborhood",street);
        for(int row=0;row<2;row++)
            for(int i=0;i<10;i++)
            {
                float x=-32+i*7.5f, z=43+row*14, height=4.5f+(i*7%5)*1.3f;
                Part(distant,"Far stepped home",new Vector3(x,3+row*2+height*.5f,z),new Vector3(6.5f,height,7),i%3==0?"Cloud plaster":"Distant neighborhood");
                Part(distant,"Far roof line",new Vector3(x,3+row*2+height+.12f,z),new Vector3(6.65f,.24f,7.15f),"Slate roof");
                for(int w=0;w<3;w++)
                    Part(distant,"Far window rhythm",new Vector3(x-1.7f+w*1.7f,3+row*2+height-.95f,z-3.52f),new Vector3(.75f,1.1f,.035f),"Window glass");
            }
        Combine(distant);
    }

    static void V3Shop(string name,Vector3 position,float yaw,float width,float height,string color,int index)
    {
        var g=Group(name+" - neighborhood shop house",street);
        g.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
        float foundationDepth=position.y+3.30f;
        Part(g,"Street block foundation",new Vector3(0,-foundationDepth*.5f,-2.6f),new Vector3(width,foundationDepth,5.2f),"Sidewalk stone");
        Part(g,"Painted upper storey",new Vector3(0,height*.5f,-2.6f),new Vector3(width,height,5.2f),color);
        Part(g,"Stone shop surround",new Vector3(0,1.38f,.025f),new Vector3(width,2.76f,.13f),"Cloud plaster");
        Part(g,"Recessed storefront",new Vector3(-.65f,1.38f,.11f),new Vector3(width-2.1f,2.12f,.05f),"Window glass");
        for(int i=0;i<3;i++)
            Part(g,"Shop interior shelf",new Vector3(-.65f,.55f+i*.57f,.16f),new Vector3(width-2.2f,.065f,.02f),"Dark joinery");
        for(int i=0;i<5;i++)
        {
            float x=-width*.5f+.55f+i*.63f;
            // Distinct little books and ceramics provide shopfront identity.
            if(index%2==0)
            {
                Part(g,"Books in the window",new Vector3(x,.77f,.195f),new Vector3(.20f,.37f,.10f),i%2==0?"Book vermilion":"Cream trim");
                Part(g,"Small window book",new Vector3(x+.22f,.71f,.195f),new Vector3(.10f,.25f,.10f),"Sea green");
            }
            else Part(g,"Ceramic window display",new Vector3(x,.73f,.22f),new Vector3(.24f,.18f,.20f),i%2==0?"Mural apricot":"Cream trim",PrimitiveType.Cylinder);
        }
        foreach(float x in new[]{-width*.5f+.23f,width*.5f-1.38f})
            Part(g,"Shopfront jamb",new Vector3(x,1.45f,.20f),new Vector3(.12f,2.35f,.16f),"Dark joinery");
        Part(g,"Shop glazing middle rail",new Vector3(-.65f,1.31f,.21f),new Vector3(width-2.05f,.065f,.10f),"Dark joinery");
        Part(g,"Shop door frame",new Vector3(width*.5f-.71f,1.32f,.13f),new Vector3(1.10f,2.45f,.20f),"Cream trim");
        Part(g,"Shop door",new Vector3(width*.5f-.71f,1.26f,.25f),new Vector3(.87f,2.25f,.10f),"Dark joinery");
        Part(g,"Shop door light",new Vector3(width*.5f-.71f,1.60f,.31f),new Vector3(.61f,.99f,.025f),"Curtain glow");
        Part(g,"Shop door pull",new Vector3(width*.5f-.41f,1.08f,.34f),new Vector3(.045f,.30f,.05f),"Road paint");
        Part(g,"Shop fascia",new Vector3(0,2.63f,.18f),new Vector3(width+.06f,.36f,.30f),"Dark joinery");
        for(int i=0;i<15;i++)
        {
            float x=-width*.5f+width*(i+.5f)/15;
            var awning=Part(g,"Canvas awning stripe",new Vector3(x,2.36f,.64f),new Vector3(width/15,.065f,1.01f),i%2==0?"Cream trim":color);
            awning.transform.localRotation=Quaternion.Euler(12,0,0);
            Part(g,"Canvas awning edge",new Vector3(x,2.19f,1.13f),new Vector3(width/15,.17f,.045f),i%2==0?"Cream trim":color);
        }
        // A recessed sign medallion reads as an authored storefront from above.
        Part(g,"Shop sign medallion",new Vector3(-1.10f,2.64f,.35f),new Vector3(.23f,.23f,.06f),"Road paint",PrimitiveType.Sphere);
        Part(g,"Shop sign inset line",new Vector3(.35f,2.64f,.345f),new Vector3(1.75f,.055f,.026f),"Cream trim");
        for(float y=2.95f;y<height-.22f;y+=.30f)
            Part(g,"Upper clapboard course",new Vector3(0,y,.028f),new Vector3(width,.027f,.05f),color);
        foreach(float x in new[]{-width*.5f+.13f,width*.5f-.13f})
            Part(g,"Painted corner board",new Vector3(x,height*.5f,.08f),new Vector3(.17f,height,.13f),"Cream trim");
        int floors=height>6.5f?2:1;
        for(int floor=0;floor<floors;floor++)
        {
            float y=3.88f+floor*1.80f;
            for(int i=0;i<3;i++)
            {
                float x=-width*.32f+i*width*.32f;
                if(i==1)
                {
                    Part(g,"Small projecting bay body",new Vector3(x,y,.25f),new Vector3(1.35f,1.75f,.50f),color);
                    Window(g,new Vector3(x,y,.52f),0,1.19f,1.34f,(index+floor)%2==0);
                    Part(g,"Small bay crown",new Vector3(x,y+.87f,.25f),new Vector3(1.58f,.14f,.73f),"Cream trim");
                    Part(g,"Small bay sill",new Vector3(x,y-.78f,.25f),new Vector3(1.53f,.13f,.70f),"Cream trim");
                }
                else Window(g,new Vector3(x,y,.10f),0,1.12f,1.34f,(index+i+floor)%3==0);
            }
        }
        Part(g,"Molded roof cornice",new Vector3(0,height,.13f),new Vector3(width+.34f,.22f,.42f),"Cream trim");
        Part(g,"Roof coping",new Vector3(0,height+.10f,-2.53f),new Vector3(width+.18f,.16f,5.4f),"Slate roof");
        for(int i=0;i<12;i++)
            Part(g,"Cornice dentil",new Vector3(-width*.45f+i*width*.082f,height-.18f,.13f),new Vector3(.12f,.19f,.23f),"Cream trim");
        Part(g,"Roof vent",new Vector3(1.8f,height+.35f,-3.6f),new Vector3(.48f,.65f,.60f),"Warm brick");
        Combine(g);
    }

    static void V3Courtyard(bool levelBlock = false)
    {
        var g=Group("V3 - courtyard and corner details",street);
        foreach(float z in new[]{1.5f,10.7f,19.6f}) V3Bench(g,new Vector3(16.5f,0,z),270);
        V3Bench(g,new Vector3(-.4f,0,levelBlock ? 19.5f : 22.5f),180);
        foreach(var p in new[]{new Vector3(16.5f,0,5.6f),new Vector3(16.5f,0,15.5f),new Vector3(-6.6f,0,levelBlock ? 19.5f : 22)})
        {
            Part(g,"Terracotta tree planter",p+Vector3.up*.29f,new Vector3(.9f,.58f,.9f),"Planter clay");
            Part(g,"Planter soil",p+Vector3.up*.59f,new Vector3(.79f,.025f,.79f),"Coffee brown");
            Part(g,"Small tree trunk",p+Vector3.up*1.34f,new Vector3(.12f,.79f,.12f),"Coffee brown",PrimitiveType.Cylinder);
            foreach(var v in new[]{new Vector3(-.32f,2.15f,0),new Vector3(.36f,2.25f,.09f),new Vector3(0,2.72f,0)})
                Part(g,"Courtyard tree foliage",p+v,new Vector3(1.14f,1.10f,1.07f),"Leaf green",PrimitiveType.Sphere);
        }
        // A long low art wall belongs to the courtyard, away from café routes.
        var mural=Group("Courtyard neighborhood mural",g);
        mural.SetPositionAndRotation(new Vector3(17.75f,0,13.0f),Quaternion.Euler(0,270,0));
        Part(mural,"Mural plaster panel",new Vector3(0,1.48f,0),new Vector3(4.6f,2.96f,.17f),"Cloud plaster");
        Part(mural,"Mural warm sky",new Vector3(0,1.75f,.10f),new Vector3(4.25f,1.72f,.016f),"Mural apricot");
        Part(mural,"Mural sunset circle",new Vector3(-.9f,2.12f,.12f),new Vector3(.98f,.98f,.018f),"Road paint",PrimitiveType.Sphere);
        for(int i=0;i<5;i++)
        {
            var hill=Part(mural,"Mural layered hill",new Vector3(-1.65f+i*.77f,.83f+i*.13f,.14f),new Vector3(1.15f,.92f,.023f),i%2==0?"Mural indigo":"Sea green");
            hill.transform.localRotation=Quaternion.Euler(0,0,-18+i*3);
        }
        Part(mural,"Mural cap",new Vector3(0,3.0f,0),new Vector3(4.77f,.13f,.29f),"Cream trim");
        // Bike hoops and a noticeboard give the sidewalk everyday purposes.
        foreach(float z in new[]{-.2f,1.0f,2.2f})
        {
            foreach(float x in new[]{13.1f,13.75f}) Part(g,"Cycle stand upright",new Vector3(x+(levelBlock ? 4.1f : 0),.43f,z),new Vector3(.045f,.86f,.045f),"Ironwork");
            Part(g,"Cycle stand rail",new Vector3(13.425f+(levelBlock ? 4.1f : 0),.88f,z),new Vector3(.69f,.06f,.045f),"Ironwork");
        }
        foreach(float x in levelBlock ? new[]{-6.6f,6.6f} : new[]{-6.6f,14.9f})
        {
            Part(g,"Corner bollard",new Vector3(x,.44f,levelBlock ? -4.7f : -6.4f),new Vector3(.13f,.44f,.13f),"Ironwork",PrimitiveType.Cylinder);
            Part(g,"Bollard brass ring",new Vector3(x,.72f,levelBlock ? -4.7f : -6.4f),new Vector3(.145f,.04f,.145f),"Road paint",PrimitiveType.Cylinder);
        }
        Part(g,"Community noticeboard frame",new Vector3(9.1f,1.24f,-3.0f),new Vector3(.13f,1.35f,1.25f),"Dark joinery");
        Part(g,"Community noticeboard cork",new Vector3(9.02f,1.24f,-3.0f),new Vector3(.027f,1.20f,1.10f),"Saffron");
        for(int i=0;i<4;i++)
        {
            var paper=Part(g,"Local event flyer",new Vector3(8.995f,1.0f+i%2*.48f,-3.26f+i/2*.50f),new Vector3(.01f,.36f,.34f),i%2==0?"Paper cream":"Sky blue");
            paper.transform.localRotation=Quaternion.Euler(i%2==0?5:-6,0,0);
        }
        foreach(float z in new[]{-3.48f,-2.52f}) Part(g,"Noticeboard post",new Vector3(9.1f,.70f,z),new Vector3(.075f,1.4f,.075f),"Dark joinery");
        Combine(g);
    }

    static void V3Bench(Transform parent,Vector3 position,float yaw)
    {
        var g=Group("Neighborhood slatted bench",parent);g.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
        for(int i=0;i<5;i++) Part(g,"Bench seat slat",new Vector3(0,.46f,-.22f+i*.11f),new Vector3(1.65f,.075f,.085f),"Saffron");
        for(int i=0;i<4;i++) Part(g,"Bench back slat",new Vector3(0,.62f+i*.115f,-.26f),new Vector3(1.65f,.085f,.055f),"Saffron");
        foreach(float x in new[]{-.62f,.62f})
        {
            foreach(float z in new[]{-.23f,.22f}) Part(g,"Bench cast leg",new Vector3(x,.225f,z),new Vector3(.065f,.45f,.065f),"Ironwork");
            Part(g,"Bench back upright",new Vector3(x,.72f,-.29f),new Vector3(.055f,.55f,.055f),"Ironwork");
            Part(g,"Bench arm",new Vector3(x,.68f,.02f),new Vector3(.075f,.055f,.53f),"Ironwork");
        }
    }

    static void V3CafeWindows(Transform rightWall)
    {
        // Keep the original full-height wall collider as the window boundary.
        rightWall.GetComponent<Renderer>().enabled=false;
        var g=Group("V3 - courtyard-facing cafe windows",street);
        Part(g,"Courtyard sill",new Vector3(7.52f,.39f,9),new Vector3(.24f,.78f,18.25f),"Dark joinery");
        Part(g,"Courtyard window header",new Vector3(7.52f,3.04f,9),new Vector3(.24f,.32f,18.25f),"Dark joinery");
        foreach(float z in new[]{0f,2.6f,4.3f,5.1f,6.9f,9.1f,12.2f,15.2f,18.0f})
            Part(g,"Courtyard window post",new Vector3(7.52f,1.69f,z),new Vector3(.21f,2.6f,.16f),"Dark joinery");
        // These solid piers retain the existing local art in its original place.
        Part(g,"Small print plaster pier",new Vector3(7.52f,1.79f,3.5f),new Vector3(.24f,2.02f,1.15f),"Cloud plaster");
        Part(g,"Local art plaster pier",new Vector3(7.52f,1.79f,6f),new Vector3(.24f,2.02f,1.8f),"Cloud plaster");
        Part(g,"Repair end plaster pier",new Vector3(7.52f,1.79f,17.25f),new Vector3(.24f,2.02f,1.75f),"Cloud plaster");
        Part(g,"Inner window ledge",new Vector3(7.32f,.81f,9),new Vector3(.35f,.10f,18.0f),"Cream trim");
        Combine(g);
    }

    static void V3InteriorDetails()
    {
        var g=Group("V3 - cafe perimeter details",street);
        // Books stay on the existing banquette at its quiet end, not in aisles.
        for(int i=0;i<3;i++)
        {
            var book=Part(g,"Banquette reading book",new Vector3(-6.86f,.60f+i*.055f,8.05f),new Vector3(.28f,.045f,.34f),i%2==0?"Book vermilion":"Sea green");
            book.transform.localRotation=Quaternion.Euler(0,-8+i*9,0);
        }
        Part(g,"Reading book page edges",new Vector3(-6.86f,.714f,8.05f),new Vector3(.255f,.025f,.322f),"Paper cream");
        // The shelf sits above rear storage, leaving repair tools and artwork free.
        Part(g,"Back bar cup shelf",new Vector3(.1f,1.43f,17.80f),new Vector3(2.25f,.095f,.32f),"Dark joinery");
        foreach(float x in new[]{-.7f,.9f})
        {
            Part(g,"Cup shelf bracket",new Vector3(x,1.29f,17.80f),new Vector3(.055f,.23f,.21f),"Ironwork");
            Part(g,"Cup shelf wall mount",new Vector3(x,1.40f,17.92f),new Vector3(.065f,.38f,.055f),"Ironwork");
        }
        for(int i=0;i<6;i++)
        {
            var cup=Group("Shelf mug",g);cup.position=new Vector3(-.78f+i*.28f,1.54f,17.74f);
            Part(cup,"Ceramic mug",Vector3.zero,new Vector3(.15f,.072f,.15f),i%2==0?"Paper cream":"Mural apricot",PrimitiveType.Cylinder);
            Part(cup,"Mug inner shadow",new Vector3(0,.073f,0),new Vector3(.113f,.005f,.113f),"Coffee brown",PrimitiveType.Cylinder);
            // Open handles are three narrow pieces rather than a solid blob.
            Part(cup,"Handle top",new Vector3(.104f,.039f,0),new Vector3(.074f,.026f,.027f),"Paper cream");
            Part(cup,"Handle outer",new Vector3(.134f,0,0),new Vector3(.022f,.078f,.027f),"Paper cream");
            Part(cup,"Handle base",new Vector3(.104f,-.039f,0),new Vector3(.074f,.026f,.027f),"Paper cream");
        }
        foreach(float x in new[]{-1.42f,-1.07f})
        {
            Part(g,"Coffee bean jar",new Vector3(x,1.11f,17.32f),new Vector3(.22f,.15f,.22f),"Coffee brown",PrimitiveType.Cylinder);
            Part(g,"Jar lid",new Vector3(x,1.275f,17.32f),new Vector3(.235f,.025f,.235f),"Cream trim",PrimitiveType.Cylinder);
            Part(g,"Jar paper label",new Vector3(x,1.11f,17.207f),new Vector3(.12f,.15f,.01f),"Paper cream");
        }
        // A cup emblem above the doorway identifies the cafe from the street.
        Part(g,"Cafe exterior sign board",new Vector3(0,2.85f,-.285f),new Vector3(2.15f,.47f,.09f),"Dark joinery");
        Part(g,"Cafe sign ceramic cup icon",new Vector3(-.64f,2.83f,-.341f),new Vector3(.23f,.21f,.025f),"Cream trim");
        Part(g,"Cafe sign cup handle",new Vector3(-.48f,2.85f,-.342f),new Vector3(.10f,.13f,.024f),"Road paint");
        Part(g,"Cafe sign saucer",new Vector3(-.64f,2.70f,-.344f),new Vector3(.33f,.04f,.025f),"Cream trim");
        for(int i=0;i<3;i++)
            Part(g,"Cafe sign warm rays",new Vector3(-.73f+i*.10f,2.995f,-.343f),new Vector3(.024f,.075f,.025f),"Road paint");
        Part(g,"Cafe sign lettering inset",new Vector3(.32f,2.87f,-.342f),new Vector3(.98f,.055f,.025f),"Cream trim");
        Part(g,"Cafe sign lower inset",new Vector3(.32f,2.75f,-.342f),new Vector3(.62f,.035f,.025f),"Road paint");
        // Patio reading material adds a small personal touch without another plant.
        var paper=Part(g,"Patio folded neighborhood paper",new Vector3(-4.12f,.746f,-1.66f),new Vector3(.22f,.012f,.19f),"Paper cream");
        paper.transform.localRotation=Quaternion.Euler(0,13,0);
        Combine(g);
    }

    static void V3Lighting(bool levelBlock = false)
    {
        var geometry=Group("V3 - lantern fixtures",street);
        var lights=Group("V3 lights",street);
        Vector3[] places={new Vector3(-5.7f,0,-5.5f),new Vector3(7.5f,0,-5.5f),new Vector3(14.5f,0,4.5f),new Vector3(14.5f,0,16.8f),new Vector3(7.7f,0,22.4f),new Vector3(-4.7f,0,22.4f)};
        if(levelBlock) places=new[]{new Vector3(-5.7f,0,-4.5f),new Vector3(7.5f,0,-4.5f),new Vector3(16.2f,0,4.5f),new Vector3(16.2f,0,16.8f),new Vector3(7.7f,0,19.5f),new Vector3(-4.7f,0,19.5f)};
        foreach(var p in places)
        {
            Part(geometry,"Lantern foot",p+Vector3.up*.12f,new Vector3(.29f,.12f,.29f),"Ironwork",PrimitiveType.Cylinder);
            Part(geometry,"Lantern pole",p+Vector3.up*1.50f,new Vector3(.065f,1.50f,.065f),"Ironwork",PrimitiveType.Cylinder);
            Part(geometry,"Lantern diffuser",p+Vector3.up*3.11f,new Vector3(.29f,.38f,.29f),"Lamp glass");
            Part(geometry,"Lantern roof",p+Vector3.up*3.34f,new Vector3(.43f,.10f,.43f),"Ironwork");
            foreach(float x in new[]{-.15f,.15f}) foreach(float z in new[]{-.15f,.15f})
                Part(geometry,"Lantern upright frame",p+new Vector3(x,3.11f,z),new Vector3(.024f,.41f,.024f),"Ironwork");
            var lamp=Group("V3 warm lantern",lights);lamp.position=p+Vector3.up*2.95f;
            var l=lamp.gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(1,.73f,.43f);l.intensity=2.2f;l.range=6.3f;l.shadows=LightShadows.None;
        }
        // Low cafe sconces are below the owner's ceiling, and use no shadow maps.
        var ceiling=levelBlock ? Group("V4 - ceiling fixtures",street) : geometry;
        foreach(var p in new[]{new Vector3(-6.7f,2.65f,6.8f),new Vector3(6.7f,2.65f,10.7f),new Vector3(0,2.8f,15.2f)})
        {
            float drop=3.20f-p.y;
            Part(ceiling,"Cafe light ceiling rose",new Vector3(p.x,3.18f,p.z),new Vector3(.16f,.025f,.16f),"Ironwork",PrimitiveType.Cylinder);
            Part(ceiling,"Cafe pendant stem",p+Vector3.up*(drop*.5f),new Vector3(.025f,drop,.025f),"Ironwork");
            Part(ceiling,"Cafe pendant shade",p+Vector3.up*.065f,new Vector3(.34f,.055f,.34f),"Dark joinery",PrimitiveType.Cylinder);
            Part(ceiling,"Cafe warm light diffuser",p,new Vector3(.25f,.11f,.25f),"Lamp glass",PrimitiveType.Cylinder);
            var bulb=Group("V3 cafe warm fill",lights);bulb.position=p-Vector3.up*.1f;
            var l=bulb.gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(1,.77f,.52f);l.intensity=4f;l.range=6.8f;l.shadows=LightShadows.None;
        }
        Combine(geometry);
        if(levelBlock) Combine(ceiling);
    }

    static void V3Neighbors(StreetLife life)
    {
        var g=Group("V3 - neighborhood walking routes",street);
        var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/CustomerAnimator(.controller");
        Vector3[][] paths={
            new[]{new Vector3(-6,.015f,-4.7f),new Vector3(12,.015f,-4.7f),new Vector3(12,.015f,-5.7f),new Vector3(-6,.015f,-5.7f)},
            new[]{new Vector3(10.6f,.015f,-3.1f),new Vector3(10.6f,.015f,21.1f),new Vector3(12.1f,.015f,21.1f),new Vector3(12.1f,.015f,-3.1f)},
            new[]{new Vector3(-6,.015f,21.0f),new Vector3(12,.015f,21.0f),new Vector3(12,.015f,22.2f),new Vector3(-6,.015f,22.2f)}
        };
        string[] names={"Front promenade neighbor","Courtyard neighbor","Rear lane neighbor"};
        for(int i=0;i<paths.Length;i++)
        {
            var person=ActorModel("Assets/ThirdParty/Quaternius_ModularMen/Beach.fbx",names[i],g,.92f);
            var animator=person.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            var actor=AddActor(life,person,g,.84f+i*.08f,.13f+i*.23f,false,false,paths[i]);actor.animator=animator;actor.turnSpeed=110;
        }
        // Expand the existing bird loops around the cafe rather than keeping
        // their entire flight on the left-hand road.
        int bird=0;
        foreach(var actor in life.actors.Where(a=>a!=null && a.actor && a.actor.name.StartsWith("Passing bird")))
        {
            Vector3[] route={new Vector3(-12,8,-8),new Vector3(13,9,-5),new Vector3(16,10,23),new Vector3(-10,11,27)};
            if(actor.waypoints.Length==route.Length)
                for(int i=0;i<route.Length;i++) actor.waypoints[i].position=route[(i+bird)%route.Length];
            actor.speed=4.4f;actor.smoothRoute=true;actor.bankAngle=10;bird++;
        }
        life.RebuildRoutes();
        EditorUtility.SetDirty(life);
    }

    // Collapse authoring pieces by material, retaining one movable group per house.
    public static string ApplyV4()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!=AcesCafeLayoutSetup.ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open and save the stopped cafe layout before applying V4.");
        if(FindOptional("10 - cohesive neighborhood V4")) return "V4 already exists; edit the saved objects directly.";
        var room=Find("ACE'S CAFE - layout study 02");
        var oldStreet=Find(Marker);var surrounds=Find("09 - neighborhood surrounds V3");
        foreach(var required in new[]{"Street - paving and road markings","Street furniture"})
            if(!oldStreet.Find(required))throw new InvalidOperationException("Missing exterior group: "+required);
        foreach(var required in new[]{"V3 - connected neighborhood ground","V3 - courtyard and corner details","V3 lights","V3 - lantern fixtures"})
            if(!surrounds.Find(required))throw new InvalidOperationException("Missing exterior group: "+required);
        if(!Find("Player").GetComponent<CafeViewMode>()||!Object.FindFirstObjectByType<CafeDaylight>()||!Find("Entry apron").GetComponent<MeshFilter>())
            throw new InvalidOperationException("Cafe view, daylight and entrance apron references are required.");
        var life=oldStreet.GetComponentInChildren<StreetLife>();
        if(!life || life.actors.Count(a=>a.actor && a.wheels.Length>0)!=2)
            throw new InvalidOperationException("Expected the two existing cars before building the V4 loop.");
        mats.Clear();
        foreach(var m in AssetDatabase.LoadAllAssetsAtPath(Folder+"/Street palette.asset").OfType<Material>())mats[m.name]=m;
        if(!mats.ContainsKey("Saffron"))mats["Saffron"]=mats["Street palette"];
        hasGeometry=true;
        V3Material("Garden grass",new Color(.34f,.45f,.25f));
        V3Material("Garden sage",new Color(.48f,.57f,.34f));
        V3Material("Distant ground",new Color(.34f,.41f,.36f));
        var paving=mats["Sidewalk stone"];
        paving.SetColor("_BaseColor",new Color(.65f,.65f,.59f));
        paving.SetFloat("_Smoothness",.12f);
        EditorUtility.SetDirty(paving);
        street=Group("10 - cohesive neighborhood V4",room);
        street.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild=true;
        var v4=street;
        V4Ground();
        V4Planting();
        // Replace only the earlier exterior paving and furniture groups. Mesh
        // subassets remain available to the saved pre-V4 scene backup.
        Object.DestroyImmediate(oldStreet.Find("Street - paving and road markings").gameObject);
        Object.DestroyImmediate(surrounds.Find("V3 - connected neighborhood ground").gameObject);
        Object.DestroyImmediate(oldStreet.Find("Street furniture").gameObject);
        Object.DestroyImmediate(surrounds.Find("V3 - courtyard and corner details").gameObject);
        var oldLights=surrounds.Find("V3 lights");
        var pendants=oldLights.Cast<Transform>().Where(t=>t.name=="V3 table pendant").ToArray();
        foreach(var p in pendants)p.SetParent(v4,true);
        Object.DestroyImmediate(oldLights.gameObject);
        Object.DestroyImmediate(surrounds.Find("V3 - lantern fixtures").gameObject);
        StreetFurniture(true);V3Courtyard(true);V3Lighting(true);
        var lightRoot=v4.Find("V3 lights");foreach(var p in pendants)p.SetParent(lightRoot,true);
        // Near streets and shop thresholds share the cafe's grade. The distant
        // hill stays behind this block instead of becoming a wall beside it.
        foreach(Transform t in oldStreet)
            if(t.name.EndsWith("bay-window house")){var p=t.position;p.y=0;t.position=p;}
        foreach(Transform t in surrounds)
            if(t.name.Contains("neighborhood shop house")){var p=t.position;p.y=0;t.position=p;}
        Find("Entry apron").GetComponent<Renderer>().sharedMaterial=paving;
        V4MapApron();
        V4Traffic(life,v4);
        V4Walkers(life);
        life.RebuildRoutes();
        var mode=Find("Player").GetComponent<CafeViewMode>();
        mode.overheadFixtures=pendants.SelectMany(t=>t.GetComponentsInChildren<Renderer>(true))
            .Concat(v4.Find("V4 - ceiling fixtures").GetComponentsInChildren<Renderer>(true)).ToArray();
        var daylight=Object.FindFirstObjectByType<CafeDaylight>();
        daylight.warmLights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
            .Where(l=>l!=daylight.sun&&(l.name=="Light_ShopWarmth"||l.name.StartsWith("V3"))).ToArray();
        daylight.emissiveRenderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
            .Where(r=>r.sharedMaterials.Any(m=>daylight.emissiveMaterials.Contains(m))).ToArray();
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        return "V4 applied: level connected cafe block, rounded street loop, four spaced cars, consistent paving, planted pockets and separated ceiling fixtures. Review and save.";
    }

    // All three concentric curves share corner centres. The roadway, curbs and
    // vehicle markers therefore agree exactly, including every rounded turn.
    static Vector3[] V4Loop(float offset,float y)
    {
        var points=new List<Vector3>();
        var centres=new[]{new Vector2(8.9f,-4),new Vector2(8.9f,19.5f),new Vector2(-8.2f,19.5f),new Vector2(-8.2f,-4)};
        for(int c=0;c<4;c++)for(int i=0;i<=12;i++)
        {
            float a=(-90+c*90+i*7.5f)*Mathf.Deg2Rad;
            points.Add(new Vector3(centres[c].x+Mathf.Cos(a)*(4+offset),y,centres[c].y+Mathf.Sin(a)*(4+offset)));
        }
        return points.ToArray();
    }

    static Mesh V4Surface(Transform parent,string name,Vector3[] outer,Vector3[] inner,string material)
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();int n=outer.Length;
        vertices.AddRange(outer);
        if(inner!=null)
        {
            vertices.AddRange(inner);
            for(int i=0;i<n;i++){int j=(i+1)%n;triangles.AddRange(new[]{i,j+n,j,i,i+n,j+n});}
        }
        else
        {
            Vector3 centre=Vector3.zero;foreach(var p in outer)centre+=p;centre/=n;vertices.Add(centre);
            for(int i=0;i<n;i++)triangles.AddRange(new[]{n,(i+1)%n,i});
        }
        var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
        mesh.uv=vertices.Select(p=>new Vector2(p.x,p.z)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
        var g=Group(name,parent).gameObject;g.AddComponent<MeshFilter>().sharedMesh=mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial=mats[material];return mesh;
    }

    static void V4Ground()
    {
        var g=Group("V4 - road and continuous sidewalks",street);var temporary=new List<Mesh>();
        Part(g,"Neighborhood ground",new Vector3(0,-.5f,5),new Vector3(150,.54f,150),"Distant ground");
        temporary.Add(V4Surface(g,"One way neighborhood road",V4Loop(2.8f,-.18f),V4Loop(-2.8f,-.18f),"Asphalt"));
        temporary.Add(V4Surface(g,"Cafe island paving",V4Loop(-2.8f,-.02f),null,"Sidewalk stone"));
        temporary.Add(V4Surface(g,"Shopfront sidewalk",V4Loop(5.2f,-.02f),V4Loop(2.8f,-.02f),"Sidewalk stone"));
        temporary.Add(V4Surface(g,"Outside curb top",V4Loop(2.96f,.015f),V4Loop(2.8f,.015f),"Cream trim"));
        temporary.Add(V4Surface(g,"Inside curb top",V4Loop(-2.8f,.015f),V4Loop(-2.96f,.015f),"Cream trim"));
        temporary.Add(V4Surface(g,"Outside curb face",V4Loop(2.8f,.015f),V4Loop(2.8f,-.18f),"Cream trim"));
        temporary.Add(V4Surface(g,"Inside curb face",V4Loop(-2.8f,-.18f),V4Loop(-2.8f,.015f),"Cream trim"));
        // These thresholds meet the same concrete apron; no separate elevated
        // rear road or brick retaining platform surrounds the shop anymore.
        Part(g,"Front shop threshold paving",new Vector3(8,-.17f,-13.95f),new Vector3(31,.28f,5.5f),"Sidewalk stone");
        Part(g,"Right shop threshold paving",new Vector3(17.5f,-.17f,9),new Vector3(3.4f,.28f,27),"Sidewalk stone");
        Part(g,"Rear shop threshold paving",new Vector3(2,-.17f,28.6f),new Vector3(31,.28f,4.6f),"Sidewalk stone");
        Part(g,"Far hill lower ground",new Vector3(3,1.38f,46),new Vector3(82,3.24f,15),"Distant ground");
        Part(g,"Far hill upper ground",new Vector3(3,2.38f,61),new Vector3(82,5.24f,15),"Distant ground");
        // A repeating joint rhythm unifies the cafe apron with the sidewalks.
        for(int i=0;i<7;i++)
        {
            Part(g,"Entry apron joint",new Vector3(-6+i*2,.004f,-2.025f),new Vector3(.015f,.006f,3.95f),"Slate roof");
            Part(g,"Promenade paving joint",new Vector3(-6+i*2,-.012f,-4.6f),new Vector3(.015f,.006f,1.1f),"Slate roof");
        }
        foreach(float x in new[]{-8.5f,8.9f,17f})for(int i=0;i<10;i++)
            Part(g,"Side paving joint",new Vector3(x,-.012f,.2f+i*1.9f),new Vector3(1.25f,.006f,.014f),"Slate roof");
        for(int side=0;side<4;side++)
        {
            var crossing=Group("Marked crossing",g);
            crossing.position=new[]{new Vector3(0,0,-8),new Vector3(12.9f,0,8.5f),new Vector3(0,0,23.5f),new Vector3(-12.2f,0,8.5f)}[side];
            crossing.rotation=Quaternion.Euler(0,side%2==0?0:90,0);
            for(int i=0;i<7;i++)Part(crossing,"Crosswalk stripe",new Vector3(0,-.167f,-2.45f+i*.8f),new Vector3(1.9f,.008f,.35f),"Cream trim");
        }
        var arrows=new[]{new Vector3(-4,-.164f,-8),new Vector3(4,-.164f,-8),new Vector3(12.9f,-.164f,2),new Vector3(12.9f,-.164f,15),new Vector3(4,-.164f,23.5f),new Vector3(-4,-.164f,23.5f),new Vector3(-12.2f,-.164f,2),new Vector3(-12.2f,-.164f,15)};
        for(int i=0;i<arrows.Length;i++)
        {
            var arrow=Group("One way direction",g);arrow.position=arrows[i];arrow.rotation=Quaternion.Euler(0,new[]{90,0,270,180}[i/2],0);
            Part(arrow,"Arrow stem",new Vector3(0,0,-.2f),new Vector3(.11f,.008f,.85f),"Cream trim");
            foreach(float sign in new[]{-1f,1f}){var wing=Part(arrow,"Arrow head",new Vector3(sign*.13f,0,.19f),new Vector3(.085f,.008f,.42f),"Cream trim");wing.transform.localRotation=Quaternion.Euler(0,-sign*40,0);}
        }
        Combine(g);foreach(var mesh in temporary)Object.DestroyImmediate(mesh);
    }

    static void V4Planting()
    {
        var g=Group("V4 - small planted sidewalk pockets",street);
        var positions=new[]{new Vector3(1,0,-14),new Vector3(8,0,-14),new Vector3(17.2f,0,7.5f),new Vector3(3,0,28.3f)};
        var random=new System.Random(41);
        foreach(var p in positions)
        {
            Part(g,"Garden stone edging",p+Vector3.up*.035f,new Vector3(3,.10f,1.15f),"Cream trim");
            Part(g,"Recessed planted soil",p+Vector3.up*.09f,new Vector3(2.83f,.04f,.99f),"Coffee brown");
            Part(g,"Low grass cover",p+Vector3.up*.118f,new Vector3(2.75f,.025f,.92f),"Garden grass");
            for(int i=0;i<28;i++)
            {
                float x=(float)random.NextDouble()*2.55f-1.275f,z=(float)random.NextDouble()*.75f-.375f;
                var tuft=Part(g,"Small groundcover",p+new Vector3(x,.16f,z),new Vector3(.09f,.13f,.035f),i%3==0?"Garden sage":"Garden grass");
                tuft.transform.localRotation=Quaternion.Euler(15,(float)random.NextDouble()*180,12);
            }
            foreach(float x in new[]{-.95f,0,.95f})
                Part(g,"Low sage cluster",p+new Vector3(x,.23f,.03f),new Vector3(.32f,.25f,.32f),"Garden sage",PrimitiveType.Sphere);
        }
        Combine(g);
    }

    static void V4MapApron()
    {
        var filter=Find("Entry apron").GetComponent<MeshFilter>();
        if(filter.sharedMesh.name=="V4 entrance apron world texture")return;
        var mesh=Object.Instantiate(filter.sharedMesh);mesh.name="V4 entrance apron world texture";
        var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            Vector3 p=filter.transform.TransformPoint(vertices[i]);Vector3 n=filter.transform.TransformDirection(normals[i]);
            uv[i]=Mathf.Abs(n.y)>.6f?new Vector2(p.x,p.z):Mathf.Abs(n.x)>.6f?new Vector2(p.z,p.y):new Vector2(p.x,p.y);
        }
        mesh.uv=uv;AssetDatabase.AddObjectToAsset(mesh,Folder+"/Street geometry.asset");filter.sharedMesh=mesh;
    }

    static void V4Traffic(StreetLife life,Transform parent)
    {
        var cars=life.actors.Where(a=>a.actor&&a.wheels.Length>0).ToList();
        var routes=cars.Select(a=>a.waypoints.FirstOrDefault()?.parent).Where(t=>t!=null).Distinct().ToArray();
        var route=Group("V4 - shared vehicle loop",parent);
        var markers=V4Loop(0,-.17f).Select((p,i)=>{var t=Group("Road centre "+i,route);t.position=p;return t;}).ToArray();
        for(int i=2;i<4;i++)
        {
            var car=Model(Folder+"/StreetModels/"+(i==2?"sedan":"hatchback-sports")+".fbx","Neighborhood car "+(i+1),parent,new Vector3(0,1.42f,0),true);
            foreach(var r in car.GetComponentsInChildren<Renderer>())r.sharedMaterial=mats["Car atlas"];
            var actor=new StreetLife.Actor{actor=car,wheels=car.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("wheel-")).ToArray(),wheelRadius=.37f};
            life.actors.Add(actor);cars.Add(actor);
        }
        for(int i=0;i<cars.Count;i++)
        {
            var a=cars[i];a.waypoints=markers;a.speed=4;a.startPhase=i/(float)cars.Count;
            a.smoothRoute=false;a.alignToSlope=false;a.turnSpeed=180;
            a.trafficGroup="Cafe block loop";a.vehicleLength=5.2f;a.minimumGap=3;
        }
        foreach(var oldRoute in routes)Object.DestroyImmediate(oldRoute.gameObject);
    }

    static void V4Walkers(StreetLife life)
    {
        foreach(var a in life.actors)
        {
            if(!a.actor||a.wheels.Length>0||a.actor.name.StartsWith("Passing bird"))continue;
            Vector3[] points;
            if(a.actor.name.StartsWith("Walking neighbor"))points=new[]{new Vector3(-15.45f,0,-2),new Vector3(-15.45f,0,23),new Vector3(-16.05f,0,23),new Vector3(-16.05f,0,-2)};
            else if(a.actor.name.StartsWith("Front promenade"))points=new[]{new Vector3(-6,0,-3.7f),new Vector3(6,0,-3.7f),new Vector3(6,0,-4.7f),new Vector3(-6,0,-4.7f)};
            else if(a.actor.name.StartsWith("Courtyard"))points=new[]{new Vector3(8.2f,0,0),new Vector3(8.2f,0,17),new Vector3(8.9f,0,17),new Vector3(8.9f,0,0)};
            else points=new[]{new Vector3(-5,0,18.65f),new Vector3(7,0,18.65f),new Vector3(7,0,19),new Vector3(-5,0,19)};
            if(a.waypoints.Length==points.Length)for(int i=0;i<points.Length;i++)a.waypoints[i].position=points[i];
        }
    }

    // A city block has streets continuing through its junctions. The four
    // ambient cars still follow a bounded route, in the inner right-hand lane.
    public static string RefineV4Streets()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!=AcesCafeLayoutSetup.ScenePath || scene.isDirty)
            throw new InvalidOperationException("Open the saved, stopped cafe layout.");
        var v4=Find("10 - cohesive neighborhood V4");
        if(v4.Find("V4 - neighborhood street grid"))return "Street grid already authored.";
        var oldGround=v4.Find("V4 - road and continuous sidewalks");
        if(!oldGround)throw new InvalidOperationException("Expected existing V4 paving.");
        mats.Clear();foreach(var m in AssetDatabase.LoadAllAssetsAtPath(Folder+"/Street palette.asset").OfType<Material>())mats[m.name]=m;
        hasGeometry=true;street=v4;
        V3Material("Crossing paint",new Color(.87f,.87f,.78f));
        V3Material("Curb concrete",new Color(.73f,.73f,.66f));
        V3Material("Curb red",new Color(.60f,.20f,.16f));
        V3Material("Sign green",new Color(.13f,.27f,.22f));
        var g=Group("V4 - neighborhood street grid",v4);
        Part(g,"Neighborhood ground",new Vector3(0,-.52f,5),new Vector3(150,.54f,150),"Distant ground");
        // Horizontal through streets and the three remaining vertical spans
        // meet edge-to-edge, avoiding coplanar patches at intersections.
        foreach(float z in new[]{-8f,23.5f})GridRect(g,"Through street",-38,39,z-2.8f,z+2.8f,-.18f,"Asphalt");
        foreach(float x in new[]{-12.2f,12.9f})foreach(var span in new[]{new Vector2(-34,-10.8f),new Vector2(-5.2f,20.7f),new Vector2(26.3f,37)})
            GridRect(g,"Cross street",x-2.8f,x+2.8f,span.x,span.y,-.18f,"Asphalt");
        float[] xs={-38,-15,-9.4f,10.1f,15.7f,39},zs={-34,-10.8f,-5.2f,20.7f,26.3f,37};
        for(int ix=0;ix<5;ix+=2)for(int iz=0;iz<5;iz+=2)
        {
            if(ix==2&&iz==2)continue;
            GridRect(g,"Connected concrete block",xs[ix],xs[ix+1],zs[iz],zs[iz+1],-.02f,"Sidewalk stone");
            // Only street-facing block edges receive raised curbs.
            if(ix>0)GridRect(g,"Block curb",xs[ix],xs[ix]+.14f,zs[iz],zs[iz+1],.02f,"Curb concrete",.20f);
            if(ix<4)GridRect(g,"Block curb",xs[ix+1]-.14f,xs[ix+1],zs[iz],zs[iz+1],.02f,"Curb concrete",.20f);
            if(iz>0)GridRect(g,"Block curb",xs[ix],xs[ix+1],zs[iz],zs[iz]+.14f,.02f,"Curb concrete",.20f);
            if(iz<4)GridRect(g,"Block curb",xs[ix],xs[ix+1],zs[iz+1]-.14f,zs[iz+1],.02f,"Curb concrete",.20f);
        }
        var temporary=new List<Mesh>();
        temporary.Add(V4Surface(g,"Cafe block pavement",V4Loop(-2.8f,-.02f),null,"Sidewalk stone"));
        temporary.Add(V4Surface(g,"Cafe curb top",V4Loop(-2.8f,.02f),V4Loop(-2.95f,.02f),"Curb concrete"));
        temporary.Add(V4Surface(g,"Cafe curb face",V4Loop(-2.8f,-.18f),V4Loop(-2.8f,.02f),"Curb concrete"));
        foreach(float z in new[]{-8f,23.5f})foreach(var span in new[]{new Vector2(-38,-18.6f),new Vector2(-5.8f,6.5f),new Vector2(19.3f,39)})
            foreach(float dz in new[]{-.065f,.065f})GridRect(g,"Double yellow",span.x,span.y,z+dz-.023f,z+dz+.023f,-.17f,"Road paint",.006f);
        foreach(float x in new[]{-12.2f,12.9f})foreach(var span in new[]{new Vector2(-34,-14.4f),new Vector2(-1.6f,17.1f),new Vector2(29.9f,37)})
            foreach(float dx in new[]{-.065f,.065f})GridRect(g,"Double yellow",x+dx-.023f,x+dx+.023f,span.x,span.y,-.17f,"Road paint",.006f);
        foreach(float x in new[]{-12.2f,12.9f})foreach(float z in new[]{-8f,23.5f})
        {
            for(int side=0;side<4;side++)
            {
                var c=Group("Junction crossing",g);c.position=new Vector3(x,0,z);c.rotation=Quaternion.Euler(0,side*90,0);
                for(int i=0;i<8;i++)Part(c,"Zebra stripe",new Vector3(-2.45f+i*.7f,-.165f,4.15f),new Vector3(.36f,.008f,1.8f),"Crossing paint");
                Part(c,"Stop line",new Vector3(-1.42f,-.163f,5.6f),new Vector3(2.55f,.008f,.18f),"Crossing paint");
                Part(c,"Drain frame",new Vector3(-2.60f,-.162f,7.2f),new Vector3(.34f,.01f,.75f),"Ironwork");
                for(int i=0;i<5;i++)Part(c,"Drain slots",new Vector3(-2.60f,-.151f,6.94f+i*.13f),new Vector3(.26f,.012f,.045f),"Slate roof");
            }
            var sign=Group("Corner street sign",g);sign.position=new Vector3(x+3.25f,0,z+3.35f);
            Part(sign,"Pole",new Vector3(0,1.2f,0),new Vector3(.06f,2.4f,.06f),"Ironwork");
            var stop=Part(sign,"Octagonal stop sign",new Vector3(0,1.72f,-.035f),new Vector3(.46f,.025f,.46f),"Curb red",PrimitiveType.Cylinder);
            stop.transform.localRotation=Quaternion.Euler(90,0,0);
            Part(sign,"Street name blade",new Vector3(.20f,2.3f,0),new Vector3(.82f,.15f,.055f),"Sign green");
            Part(sign,"Cross street blade",new Vector3(0,2.46f,.2f),new Vector3(.055f,.15f,.82f),"Sign green");
            // Small generic plaques remain prop detail rather than HUD labels.
            Part(sign,"Street plaque inset",new Vector3(.2f,2.3f,-.031f),new Vector3(.62f,.025f,.008f),"Crossing paint");
        }
        Part(g,"Far hill lower ground",new Vector3(3,1.38f,46),new Vector3(82,3.24f,15),"Distant ground");
        Part(g,"Far hill upper ground",new Vector3(3,2.38f,61),new Vector3(82,5.24f,15),"Distant ground");
        Combine(g);foreach(var mesh in temporary)Object.DestroyImmediate(mesh);
        Object.DestroyImmediate(oldGround.gameObject);
        // Make real openings in the surrounding frontage for through streets.
        var houses=Find(Marker).Cast<Transform>().Where(t=>t.name.EndsWith("bay-window house")).OrderBy(t=>t.name).ToArray();
        float[] hz={-2,4,10,16,30.6f,36.6f};
        for(int i=0;i<houses.Length;i++)houses[i].position=new Vector3(-17.55f,0,hz[i]);
        var surrounds=Find("09 - neighborhood surrounds V3");
        foreach(string prefix in new[]{"Front - ","Rear hill - "})
        {
            var row=surrounds.Cast<Transform>().Where(t=>t.name.StartsWith(prefix)).OrderBy(t=>t.name).ToArray();
            float[] positions={-5.7f,.35f,6.4f,19.5f,25.55f};
            for(int i=0;i<row.Length;i++)row[i].position=new Vector3(positions[i],0,prefix.StartsWith("Front")?-16.55f:30.8f);
        }
        // The bed formerly sat in what is now an eastbound through street.
        var oldBeds=v4.Find("V4 - small planted sidewalk pockets");if(oldBeds)Object.DestroyImmediate(oldBeds.gameObject);
        V4Planting();
        var life=Object.FindFirstObjectByType<StreetLife>();var cars=life.actors.Where(a=>a.actor&&a.wheels.Length>0).ToArray();
        var oldRoutes=cars.Select(a=>a.waypoints[0].parent).Distinct().ToArray();
        var route=Group("V4 - right lane around neighborhood block",v4);
        var points=V4Loop(-1.4f,-.17f).Reverse().ToArray();
        var markers=points.Select((p,i)=>{var t=Group("Right lane "+i,route);t.position=p;return t;}).ToArray();
        var stopPositions=new[]{new Vector3(-4.95f,-.17f,-6.6f),new Vector3(-10.8f,-.17f,16.25f),new Vector3(5.65f,-.17f,22.1f),new Vector3(11.5f,-.17f,-.75f)};
        var stops=stopPositions.Select((p,i)=>{var t=Group("Stop approach "+i,route);t.position=p;return t;}).ToArray();
        for(int i=0;i<cars.Length;i++)
        {
            var a=cars[i];a.waypoints=markers;a.stopWaypoints=stops;a.stopDuration=1.25f;
            a.speed=3.4f+i*.13f;a.startPhase=i/(float)cars.Length;a.trafficGroup="Neighborhood right lane";
            a.vehicleLength=5.2f;a.minimumGap=3;a.turnSpeed=180;
        }
        foreach(var old in oldRoutes)Object.DestroyImmediate(old.gameObject);
        // Keep walkers on their original safe block; the left route turns before the new crossing.
        foreach(var a in life.actors.Where(a=>a.actor&&a.actor.name.StartsWith("Walking neighbor")))
        {float[] z={-2,19.2f,19.2f,-2};for(int i=0;i<4;i++)a.waypoints[i].position=new Vector3(i<2?-15.6f:-16.2f,0,z[i]);}
        life.RebuildRoutes();EditorUtility.SetDirty(life);
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        return "Street grid authored: four through junctions, double-yellow lanes, crossings, stop approaches, opened building frontage and spaced right-lane traffic.";
    }

    static void GridRect(Transform parent,string name,float x0,float x1,float z0,float z1,float top,string material,float depth=.10f)
        => Part(parent,name,new Vector3((x0+x1)*.5f,top-depth*.5f,(z0+z1)*.5f),new Vector3(x1-x0,depth,z1-z0),material);

    public static string ApplyV4SurfaceTextures()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!=AcesCafeLayoutSetup.ScenePath)
            throw new InvalidOperationException("Open the stopped cafe layout.");
        foreach(string name in new[]{"concrete_pavement_color","concrete_pavement_normal","oak_veneer_01_color","oak_veneer_01_normal","bitumen_color","bitumen_normal","fabric_pattern_07_normal","sparse_grass_color","sparse_grass_normal"})
        {
            string path=Folder+"/Textures/"+name+".jpg";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Missing texture "+path);
            importer.textureType=name.EndsWith("normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!name.EndsWith("normal");importer.maxTextureSize=1024;
            importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;
            importer.anisoLevel=4;importer.textureCompression=TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
        mats.Clear();foreach(var m in AssetDatabase.LoadAllAssetsAtPath(Folder+"/Street palette.asset").OfType<Material>())mats[m.name]=m;
        Surface(mats["Sidewalk stone"],"concrete_pavement",.556f,.36f,.12f,new Color(.95f,.97f,.95f));
        Surface(mats["Slate roof"],"bitumen",.12f,.38f,.16f,new Color(.87f,.94f,1));
        Surface(mats["Asphalt"],"asphalt_floor",.34f,.42f,.07f,new Color(.50f,.56f,.61f));
        Surface(mats["Dark joinery"],"oak_veneer_01",.55f,.20f,.26f,new Color(.30f,.48f,.44f));
        Surface(mats["Cloud plaster"],"painted_plaster_wall",.7f,.35f,.1f,null);
        Surface(mats["Garden grass"],"sparse_grass",.5f,.25f,.05f,new Color(.87f,1,.75f));
        // Do not apply wood grain to shared cream road paint or colored artwork.
        foreach(string name in new[]{"Street palette","Dusty rose","Sea green","Lavender","Sky blue","Warm brick"})
            if(mats.TryGetValue(name,out var m))Surface(m,"painted_plaster_wall",.75f,.40f,.14f,null);
        var cafe=AssetDatabase.LoadAllAssetsAtPath(Folder+"/Cafe palette.asset").OfType<Material>().ToDictionary(m=>m.name);
        Surface(cafe["Oat plaster"],"painted_plaster_wall",.7f,.28f,.1f,null);
        var oak=cafe.TryGetValue("Honey oak",out var honey)?honey:cafe["Cafe palette"];
        Surface(oak,"oak_veneer_01",.55f,.20f,.3f,new Color(.91f,.75f,.57f));
        var runner=SceneMaterial(cafe["Sage joinery"],"Woven cafe runner");
        Surface(runner,"fabric_pattern_07",2.5f,.35f,.02f,null,true);
        Find("Central woven runner").GetComponent<Renderer>().sharedMaterial=runner;
        Surface(cafe["Sage joinery"],"oak_veneer_01",.55f,.06f,.26f,null,true);
        var ceiling=SceneMaterial(cafe["Oat plaster"],"Soft ceiling plaster");
        Surface(ceiling,"painted_plaster_wall",.7f,.08f,.05f,new Color(.90f,.84f,.73f),true);
        var ceilingObject=Find("Inside-only ceiling");ceilingObject.GetComponent<Renderer>().sharedMaterial=ceiling;
        var ceilingFilter=ceilingObject.GetComponent<MeshFilter>();
        if(!ceilingFilter.sharedMesh.name.StartsWith("Surface UV"))
        {
            var mesh=Object.Instantiate(ceilingFilter.sharedMesh);mesh.name="Surface UV - ceiling";
            WorldUV(ceilingFilter,mesh);AssetDatabase.AddObjectToAsset(mesh,Folder+"/Street geometry.asset");ceilingFilter.sharedMesh=mesh;
        }
        var root=Find("ACE'S CAFE - layout study 02");var replacements=new Dictionary<Material,Material>();
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials=r.sharedMaterials;bool changed=false;
            for(int i=0;i<materials.Length;i++)
            {
                var original=materials[i];if(!original||!original.name.StartsWith("CC_Wood_"))continue;
                if(!replacements.TryGetValue(original,out var replacement))
                {
                    replacement=SceneMaterial(original,"Cafe grain - "+original.name);
                    bool top=original.name.Contains("Counter");
                    Surface(replacement,"oak_veneer_01",1f,.22f,top?.33f:.24f,top?new Color(1,.89f,.72f):new Color(.63f,.48f,.35f));
                    replacements.Add(original,replacement);
                }
                materials[i]=replacement;changed=true;
            }
            if(changed){r.sharedMaterials=materials;EditorUtility.SetDirty(r);}
        }
        int mapped=0;
        foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var r=f.GetComponent<Renderer>();if(!r||!f.sharedMesh)continue;
            // Preserve imported UVs where present. The authored counter FBX
            // has none, so give scene-specific copies a usable grain direction.
            bool counter=r.sharedMaterials.Any(m=>m&&m.name.StartsWith("Cafe grain - "))&&f.sharedMesh.uv.Length==0;
            bool primitive=f.sharedMesh.name=="Cube"&&r.sharedMaterials.Any(m=>m==oak||m==cafe["Sage joinery"]||m==cafe["Oat plaster"]||m==runner);
            if(!counter&&!primitive)continue;
            var mesh=Object.Instantiate(f.sharedMesh);mesh.name="Surface UV - "+f.name;
            WorldUV(f,mesh,counter);AssetDatabase.AddObjectToAsset(mesh,Folder+"/Street geometry.asset");f.sharedMesh=mesh;mapped++;
        }
        foreach(var mesh in AssetDatabase.LoadAllAssetsAtPath(Folder+"/Street geometry.asset").OfType<Mesh>())
            if(mesh.uv.Length==mesh.vertexCount&&mesh.normals.Length==mesh.vertexCount){mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        return "Surface textures applied; "+mapped+" primitive surfaces mapped in metres; imported furniture materials copied locally.";
    }

    static Material SceneMaterial(Material source,string name)
    {
        var old=AssetDatabase.LoadAllAssetsAtPath(Folder+"/Cafe palette.asset").OfType<Material>().FirstOrDefault(m=>m.name==name);
        if(old)return old;
        var material=new Material(source){name=name,enableInstancing=true};
        AssetDatabase.AddObjectToAsset(material,Folder+"/Cafe palette.asset");return material;
    }

    public static string AddV4ReadingShelf()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=AcesCafeLayoutSetup.ScenePath)
            throw new InvalidOperationException("Open the stopped cafe scene.");
        if(FindOptional("11 - neighborhood reading shelf"))return "Reading shelf already placed.";
        if(!AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/CafeBookcase.fbx"))throw new InvalidOperationException("Import the reviewed bookcase first.");
        var cafe=AssetDatabase.LoadAllAssetsAtPath(Folder+"/Cafe palette.asset").OfType<Material>().ToDictionary(m=>m.name);
        var materials=new Dictionary<string,Material>();
        var oak=SceneMaterial(cafe["Cafe grain - CC_Wood_Counter"],"Reading shelf oak");
        Surface(oak,"oak_veneer_01",1,.16f,.25f,new Color(.92f,.88f,.76f));materials["BookshelfOak"]=oak;
        var sage=SceneMaterial(cafe["Sage joinery"],"Reading shelf sage");
        Surface(sage,"oak_veneer_01",1,.04f,.2f,new Color(.29f,.38f,.31f),true);materials["BookshelfSage"]=sage;
        string[] names={"BookPaper","BookTerracotta","BookInk","BookOchre","BookBlue"};
        Color[] colors={new Color(.91f,.86f,.75f),new Color(.64f,.30f,.23f),new Color(.13f,.24f,.26f),new Color(.77f,.59f,.29f),new Color(.32f,.49f,.57f)};
        for(int i=0;i<names.Length;i++)
        {
            var m=SceneMaterial(cafe["Paper"],"Reading shelf - "+names[i]);
            m.SetTexture("_BaseMap",null);m.SetTexture("_BumpMap",null);m.DisableKeyword("_NORMALMAP");
            m.SetColor("_BaseColor",colors[i]);m.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(m);materials[names[i]]=m;
        }
        var root=Group("11 - neighborhood reading shelf",Find("ACE'S CAFE - layout study 02"));
        var shelf=Model(Folder+"/CafeBookcase.fbx","Oak neighborhood bookcase",root,new Vector3(1.6f,1.65f,.38f),true);
        foreach(var r in shelf.GetComponentsInChildren<Renderer>())
            r.sharedMaterials=r.sharedMaterials.Select(m=>materials.First(p=>m.name.StartsWith(p.Key)).Value).ToArray();
        shelf.SetPositionAndRotation(new Vector3(-7.16f,0,10),Quaternion.Euler(0,90,0));
        var collider=shelf.gameObject.AddComponent<BoxCollider>();
        collider.center=Vector3.up*(.825f/shelf.localScale.y);
        collider.size=new Vector3(1.6f/shelf.localScale.x,1.65f/shelf.localScale.y,.38f/shelf.localScale.z);
        // Cloth belongs on the existing banquette; it does not need more furniture.
        Surface(cafe["Moss upholstery"],"fabric_pattern_07",2.5f,.16f,.03f,null,true);
        foreach(var f in Find("Window banquette - future seated animation").GetComponentsInChildren<MeshFilter>())
        {
            if(f.sharedMesh.name!="Cube"||f.GetComponent<Renderer>().sharedMaterial!=cafe["Moss upholstery"])continue;
            var mesh=Object.Instantiate(f.sharedMesh);mesh.name="Surface UV - "+f.name;WorldUV(f,mesh);
            AssetDatabase.AddObjectToAsset(mesh,Folder+"/Street geometry.asset");f.sharedMesh=mesh;
        }
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        return "Curated oak-and-sage bookcase placed beside the window seating: 24 books, art volume, repair stack and postcard frame; one fitted collider.";
    }

    public static string FinishReadingPostcard()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the cafe first.");
        var shelf=Find("Oak neighborhood bookcase");
        if(shelf.Find("Neighborhood postcard"))return "Postcard already framed.";
        mats.Clear();foreach(var m in AssetDatabase.LoadAllAssetsAtPath(Folder+"/Cafe palette.asset").OfType<Material>())mats[m.name]=m;
        hasGeometry=true;var art=Group("Neighborhood postcard",shelf);art.localPosition=new Vector3(-.474f,1.368f,.0811f);
        // An original miniature print: three stepped bay houses and a hill.
        // These nearly-flat shapes preserve a cream border inside the frame.
        Part(art,"Postcard sky",new Vector3(0,.016f,0),new Vector3(.265f,.145f,.00012f),"Reading shelf - BookBlue");
        for(int i=0;i<3;i++)
        {
            float x=-.081f+i*.081f,baseY=-.067f+i*.018f,height=.075f+i*.012f;
            Part(art,"Painted bay house",new Vector3(x,baseY+height*.5f,.00020f),new Vector3(.064f,height,.00012f),i==0?"Reading shelf - BookTerracotta":i==1?"Reading shelf - BookOchre":"Reading shelf sage");
            Part(art,"Cornice",new Vector3(x,baseY+height,.00034f),new Vector3(.072f,.005f,.00012f),"Reading shelf - BookPaper");
            for(int row=0;row<2;row++)foreach(float side in new[]{-1f,1f})
            {
                Part(art,"Bay window trim",new Vector3(x+side*.014f,baseY+.028f+row*.026f,.00034f),new Vector3(.017f,.021f,.00012f),"Reading shelf - BookPaper");
                Part(art,"Bay window pane",new Vector3(x+side*.014f,baseY+.028f+row*.026f,.00048f),new Vector3(.011f,.015f,.00012f),"Reading shelf - BookInk");
            }
        }
        var road=Part(art,"Hill street",new Vector3(0,-.056f,.00062f),new Vector3(.254f,.009f,.00012f),"Reading shelf - BookInk");
        road.transform.localRotation=Quaternion.Euler(0,0,12.5f);
        Combine(art);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "Small original neighborhood postcard fitted inside the bookcase frame.";
    }

    public static string ApplyV4ThroughTraffic()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!=AcesCafeLayoutSetup.ScenePath||scene.isDirty)
            throw new InvalidOperationException("Open the saved stopped cafe scene.");
        var v4=Find("10 - cohesive neighborhood V4");
        if(v4.Find("V4 - through traffic"))return "Through traffic already authored.";
        mats.Clear();foreach(var m in AssetDatabase.LoadAllAssetsAtPath(Folder+"/Street palette.asset").OfType<Material>())mats[m.name]=m;
        street=v4;hasGeometry=true;
        string[] models={"sedan","hatchback-sports","taxi","van","suv","delivery","sedan-sports","suv-luxury"};
        foreach(string name in models)if(!AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/StreetModels/"+name+".fbx"))throw new InvalidOperationException("Missing car "+name);
        var life=Object.FindFirstObjectByType<StreetLife>();
        var oldCars=life.actors.Where(a=>a.actor&&a.wheels.Length>0).ToArray();
        var oldRoutes=oldCars.Select(a=>a.waypoints[0].parent).Distinct().ToArray();
        foreach(var a in oldCars){life.actors.Remove(a);Object.DestroyImmediate(a.actor.gameObject);}
        foreach(var route in oldRoutes)Object.DestroyImmediate(route.gameObject);
        var g=Group("V4 - through traffic",v4);
        life.signalGreenSeconds=14;life.signalClearanceSeconds=5;
        for(int lane=0;lane<8;lane++)
        {
            bool ns=lane<4,forward=lane%2==0;
            float constant=ns?new[]{-10.8f,-13.6f,14.3f,11.5f}[lane]:new[]{-9.4f,-6.6f,22.1f,24.9f}[lane-4];
            var route=Group((ns?"Avenue ":"Cross street ")+lane+(forward?" forward":" return"),g);
            float[] spans=ns?new[]{-180f,-70f,-34f,37f,90f,180f}:new[]{-180f,180f};
            if(!forward)Array.Reverse(spans);
            var positions=spans.Select(s=>ns?ThroughPoint(constant,s):ThroughPoint(s,constant)).ToArray();
            var markers=positions.Select((p,i)=>{var t=Group("Route "+i,route);t.position=p;return t;}).ToArray();
            float[] junctions=ns?new[]{-8f,23.5f}:new[]{-12.2f,12.9f};if(!forward)Array.Reverse(junctions);
            float sign=forward?1:-1;
            var stopPositions=junctions.Select(s=>ns?ThroughPoint(constant,s-sign*7.8f):ThroughPoint(s-sign*7.8f,constant)).ToArray();
            var stops=stopPositions.Select((p,i)=>{var t=Group("Signal stop "+i,route);t.position=p;return t;}).ToArray();
            float length=0;for(int i=1;i<positions.Length;i++)length+=Vector3.Distance(positions[i-1],positions[i]);
            for(int index=0;index<2;index++)
            {
                int variant=(lane+index*3)%models.Length;string model=models[variant];
                float height=model=="delivery"?1.8f:model=="van"?1.7f:model.StartsWith("suv")?1.6f:1.42f;
                var car=Model(Folder+"/StreetModels/"+model+".fbx","Passing "+model+" "+lane+"-"+index,g,new Vector3(0,height,0),true);
                foreach(var r in car.GetComponentsInChildren<Renderer>())r.sharedMaterial=mats["Car atlas"];
                float startCoordinate=junctions[0]-sign*(7.8f+12+index*22);
                var start=ns?ThroughPoint(constant,startCoordinate):ThroughPoint(startCoordinate,constant);
                float distance=0;
                for(int i=1;i<positions.Length;i++)
                {
                    var edge=positions[i]-positions[i-1];float t=Mathf.Clamp01(Vector3.Dot(start-positions[i-1],edge)/edge.sqrMagnitude);
                    if(Vector3.Distance(start,positions[i-1]+edge*t)<.01f){distance+=edge.magnitude*t;break;}
                    distance+=edge.magnitude;
                }
                var bounds=car.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
                life.actors.Add(new StreetLife.Actor{
                    actor=car,waypoints=markers,openRoute=true,speed=4.5f+(lane%3)*.18f+index*.1f,startPhase=distance/length,
                    smoothRoute=false,alignToSlope=ns,turnSpeed=180,trafficGroup="Through lane "+lane,
                    vehicleLength=Mathf.Max(5.2f,bounds.size.z+.3f),minimumGap=3,stopWaypoints=stops,junctionSignalPhase=ns?0:1,
                    respawnDelay=3+lane*.4f,respawnDelayVariation=8,
                    wheels=car.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("wheel-")&&(t.name.EndsWith("-left")||t.name.EndsWith("-right"))).ToArray(),wheelRadius=.33f
                });
            }
        }
        var bulb=V3Material("Traffic lamp lens",new Color(.02f,.02f,.02f));bulb.EnableKeyword("_EMISSION");
        bulb.SetColor("_EmissionColor",Color.black);EditorUtility.SetDirty(bulb);
        life.signalHeads.Clear();
        var furniture=Group("V4 - junction signal housings",v4);
        foreach(float x in new[]{-12.2f,12.9f})foreach(float z in new[]{-8f,23.5f})for(int side=0;side<4;side++)
        {
            var post=Group("Traffic signal",g);
            post.position=new Vector3(x,0,z)+new[]{new Vector3(3.25f,0,-5.2f),new Vector3(-5.2f,0,-3.25f),new Vector3(-3.25f,0,5.2f),new Vector3(5.2f,0,3.25f)}[side];
            post.rotation=Quaternion.Euler(0,side*90,0);
            var pole=Part(post,"Signal post",new Vector3(0,1.35f,0),new Vector3(.065f,2.7f,.065f),"Ironwork",PrimitiveType.Cylinder);pole.transform.SetParent(furniture,true);
            var housing=Part(post,"Signal housing",new Vector3(0,2.37f,-.07f),new Vector3(.24f,.64f,.16f),"Ironwork");housing.transform.SetParent(furniture,true);
            var lenses=new Renderer[3];
            for(int i=0;i<3;i++)lenses[i]=Part(post,"Signal lens "+i,new Vector3(0,2.55f-i*.18f,-.161f),new Vector3(.135f,.135f,.045f),"Traffic lamp lens",PrimitiveType.Sphere).GetComponent<Renderer>();
            life.signalHeads.Add(new StreetLife.SignalHead{signalGroup=side%2==0?0:1,red=lenses[0],amber=lenses[1],green=lenses[2]});
        }
        Combine(furniture);
        var oldStopSigns=v4.Find("V4 - neighborhood street grid/Curb red");if(oldStopSigns)oldStopSigns.gameObject.SetActive(false);
        ExtendV4RoadsBeyondView(v4);
        life.RebuildRoutes();EditorUtility.SetDirty(life);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
        return "16 pooled cars in eight model styles now enter and leave along eight through lanes, with synchronized junction signals and distant reuse.";
    }

    static Vector3 ThroughPoint(float x,float z)=>new Vector3(x,-.17f+Mathf.Max(0,z-37)*.20f,z);

    static void ExtendV4RoadsBeyondView(Transform parent)
    {
        var g=Group("V4 - roads beyond distance haze",parent);var temporary=new List<Mesh>();
        foreach(float x in new[]{-12.2f,12.9f})
        {
            temporary.Add(SlopePatch(g,"Distant uphill street",x-2.8f,x+2.8f,90,220,10.42f,"Asphalt"));
            GridRect(g,"Distant south street",x-2.8f,x+2.8f,-220,-70,-.18f,"Asphalt");
            foreach(float s in new[]{-1f,1f})
            {
                float a=x+s*2.8f,b=x+s*5;
                temporary.Add(SlopePatch(g,"Distant uphill paving",Mathf.Min(a,b),Mathf.Max(a,b),90,220,10.58f,"Sidewalk stone"));
                temporary.Add(SlopePatch(g,"Distant uphill line",x+s*.065f-.023f,x+s*.065f+.023f,90,220,10.43f,"Road paint"));
                GridRect(g,"Distant south paving",Mathf.Min(a,b),Mathf.Max(a,b),-220,-70,-.02f,"Sidewalk stone");
                GridRect(g,"Distant south line",x+s*.065f-.023f,x+s*.065f+.023f,-220,-70,-.17f,"Road paint",.006f);
            }
        }
        foreach(float z in new[]{-8f,23.5f})foreach(var span in new[]{new Vector2(-220,-75),new Vector2(75,220)})
        {
            GridRect(g,"Distant cross street",span.x,span.y,z-2.8f,z+2.8f,-.18f,"Asphalt");
            foreach(float s in new[]{-1f,1f})
            {
                float a=z+s*2.8f,b=z+s*5;
                GridRect(g,"Distant cross paving",span.x,span.y,Mathf.Min(a,b),Mathf.Max(a,b),-.02f,"Sidewalk stone");
                GridRect(g,"Distant cross line",span.x,span.y,z+s*.065f-.023f,z+s*.065f+.023f,-.17f,"Road paint",.006f);
            }
        }
        Combine(g);foreach(var mesh in temporary)Object.DestroyImmediate(mesh);
    }

    public static string FinishV4Backdrop()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop the cafe first.");
        var v4=Find("10 - cohesive neighborhood V4");
        if(v4.Find("V4 - distant street continuations"))return "Backdrop already refined.";
        mats.Clear();foreach(var m in AssetDatabase.LoadAllAssetsAtPath(Folder+"/Street palette.asset").OfType<Material>())mats[m.name]=m;
        street=v4;hasGeometry=true;var g=Group("V4 - distant street continuations",v4);
        // Replace the two sheer terraced blocks with one gentle background hill.
        v4.Find("V4 - neighborhood street grid/Distant ground").gameObject.SetActive(false);
        GridRect(g,"Neighborhood base",-75,75,-70,37,-.25f,"Distant ground",.1f);
        var temporary=new List<Mesh>();
        temporary.Add(SlopePatch(g,"Continuous distant hill",-75,75,37,90,-.23f,"Distant ground"));
        foreach(float x in new[]{-12.2f,12.9f})
        {
            temporary.Add(SlopePatch(g,"Uphill street",x-2.8f,x+2.8f,37,90,-.18f,"Asphalt"));
            foreach(float s in new[]{-1f,1f})
            {
                float inner=x+s*2.8f,outer=x+s*5.0f;
                temporary.Add(SlopePatch(g,"Uphill sidewalk",Mathf.Min(inner,outer),Mathf.Max(inner,outer),37,90,-.02f,"Sidewalk stone"));
                temporary.Add(SlopePatch(g,"Uphill yellow line",x+s*.065f-.023f,x+s*.065f+.023f,37,90,-.17f,"Road paint"));
            }
            GridRect(g,"Street to south",x-2.8f,x+2.8f,-70,-34,-.18f,"Asphalt");
            foreach(float s in new[]{-1f,1f})
            {
                GridRect(g,"South yellow line",x+s*.065f-.023f,x+s*.065f+.023f,-70,-34,-.17f,"Road paint",.006f);
                float inner=x+s*2.8f,outer=x+s*5;
                GridRect(g,"South sidewalk",Mathf.Min(inner,outer),Mathf.Max(inner,outer),-70,-34,-.02f,"Sidewalk stone");
            }
        }
        foreach(float z in new[]{-8f,23.5f})foreach(var span in new[]{new Vector2(-75,-38),new Vector2(39,75)})
        {
            GridRect(g,"Cross street continuation",span.x,span.y,z-2.8f,z+2.8f,-.18f,"Asphalt");
            foreach(float s in new[]{-1f,1f})
            {
                GridRect(g,"Cross street yellow line",span.x,span.y,z+s*.065f-.023f,z+s*.065f+.023f,-.17f,"Road paint",.006f);
                float inner=z+s*2.8f,outer=z+s*5;
                GridRect(g,"Cross street sidewalk",span.x,span.y,Mathf.Min(inner,outer),Mathf.Max(inner,outer),-.02f,"Sidewalk stone");
            }
        }
        Combine(g);foreach(var mesh in temporary)Object.DestroyImmediate(mesh);
        var old=Find("V3 - distant hill neighborhood");Object.DestroyImmediate(old.gameObject);
        var distant=Group("V4 - homes on the distant slope",v4);
        float[] columns={-33,-26,-19,-5.6f,0,5.6f,20,27,34};
        for(int row=0;row<2;row++)for(int i=0;i<columns.Length;i++)
        {
            float z=44+row*14,baseY=(z-37)*.20f-.35f,height=4.5f+(i*7%5)*.8f,x=columns[i];
            Part(distant,"Hill home",new Vector3(x,baseY+height*.5f,z),new Vector3(5.2f,height,7),i%3==0?"Cloud plaster":"Distant neighborhood");
            Part(distant,"Hill roof",new Vector3(x,baseY+height+.10f,z),new Vector3(5.4f,.20f,7.15f),"Slate roof");
            for(int w=0;w<3;w++)Part(distant,"Hill window",new Vector3(x-1.55f+w*1.55f,baseY+height-1,z-3.52f),new Vector3(.72f,1.05f,.035f),"Window glass");
        }
        Combine(distant);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return "Distant streets continue into haze and a gradual hill; houses leave the street corridors open.";
    }

    static Mesh SlopePatch(Transform parent,string name,float x0,float x1,float z0,float z1,float baseY,string material)
    {
        float farY=baseY+(z1-z0)*.20f;
        var mesh=new Mesh{name=name};mesh.vertices=new[]{new Vector3(x0,baseY,z0),new Vector3(x1,baseY,z0),new Vector3(x1,farY,z1),new Vector3(x0,farY,z1)};
        mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateNormals();mesh.RecalculateBounds();
        var go=Group(name,parent).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mats[material];return mesh;
    }

    static void Surface(Material material,string family,float tiling,float bump,float smooth,Color? tint,bool normalOnly=false)
    {
        var color=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/"+family+"_color.jpg");
        var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/"+family+"_normal.jpg");
        material.SetTexture("_BaseMap",normalOnly?null:color);material.SetTexture("_BumpMap",normal);
        material.SetTextureScale("_BaseMap",Vector2.one*tiling);material.SetFloat("_BumpScale",bump);
        material.SetFloat("_Smoothness",smooth);material.enableInstancing=true;
        if(normal)material.EnableKeyword("_NORMALMAP");else material.DisableKeyword("_NORMALMAP");
        if(tint.HasValue)material.SetColor("_BaseColor",tint.Value);EditorUtility.SetDirty(material);
    }

    static void WorldUV(MeshFilter filter,Mesh mesh,bool counter=false)
    {
        var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            var p=filter.transform.TransformPoint(vertices[i]);var n=filter.transform.TransformDirection(normals[i]);
            uv[i]=Mathf.Abs(n.y)>.6f?(counter?new Vector2(p.z,p.x):new Vector2(p.x,p.z)):Mathf.Abs(n.x)>.6f?new Vector2(p.z,p.y):new Vector2(p.x,p.y);
        }
        mesh.uv=uv;mesh.RecalculateTangents();
    }

    static void Combine(Transform root)
    {
        var filters=root.GetComponentsInChildren<MeshFilter>();
        foreach(var batch in filters.GroupBy(f=>f.GetComponent<Renderer>().sharedMaterial))
        {
            var temporary=new List<Mesh>();
            var instances=new List<CombineInstance>();
            foreach(var f in batch)
            {
                var mesh=Object.Instantiate(f.sharedMesh);temporary.Add(mesh);
                var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 v=f.transform.TransformPoint(vertices[i]);Vector3 n=f.transform.TransformDirection(normals[i]);
                    uv[i]=Mathf.Abs(n.y)>.6f?new Vector2(v.x,v.z):Mathf.Abs(n.x)>.6f?new Vector2(v.z,v.y):new Vector2(v.x,v.y);
                }
                mesh.uv=uv;mesh.RecalculateTangents();
                instances.Add(new CombineInstance{mesh=mesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix});
            }
            var combined=new Mesh{name=root.name+" - "+batch.Key.name,indexFormat=IndexFormat.UInt32};
            combined.CombineMeshes(instances.ToArray(),true,true);
            string path=Folder+"/Street geometry.asset";
            if(!hasGeometry){AssetDatabase.CreateAsset(combined,path);hasGeometry=true;}else AssetDatabase.AddObjectToAsset(combined,path);
            var g=Group(batch.Key.name,root).gameObject;g.AddComponent<MeshFilter>().sharedMesh=combined;
            var renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterial=batch.Key;
            foreach(var mesh in temporary)Object.DestroyImmediate(mesh);
        }
        var originals=filters.Select(f=>f.gameObject).ToArray();
        foreach(var g in originals)Object.DestroyImmediate(g);
        // Remove the now-empty authoring containers, keeping the mesh children.
        foreach(var t in root.GetComponentsInChildren<Transform>().Reverse().ToArray())
            if(t!=root&&t.childCount==0&&!t.GetComponent<Renderer>())Object.DestroyImmediate(t.gameObject);
    }
}
