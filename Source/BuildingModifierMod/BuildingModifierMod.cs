﻿using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace BuildingModifierMod
{
    
	

	[HarmonyPatch(typeof(BuildingConfigManager), "RegisterBuilding")]
	internal class BuildingModifierMod_BuildingConfigManager_RegisterBuilding
	{

		private static bool Prefix(BuildingConfigManager __instance, IBuildingConfig config)
		{
			if (!BuildingModifierState.StateManager.State.Enabled)
				return true;

			if (BuildingModifierState.StateManager.State.Debug)
				Debug.Log(" === [BuildingModifier] BuildingModifierMod_BuildingConfigManager_RegisterBuilding Prefix === ");

            FieldInfo configTableF = AccessTools.Field(typeof(BuildingConfigManager), "configTable");
			FieldInfo baseTemplateF = AccessTools.Field(typeof(BuildingConfigManager), "baseTemplate");
			FieldInfo NonBuildableBuildingsF = AccessTools.Field(typeof(BuildingConfigManager), "NonBuildableBuildings");


			BuildingDef buildingDef = config.CreateBuildingDef();
            if (!BuildingModifierState.StateManager.State.Modifiers.ContainsKey(buildingDef.PrefabID) )
            {
				if (BuildingModifierState.StateManager.State.Debug)
					Debug.Log(" === [BuildingModifier] Ignoring: " + buildingDef.PrefabID);
				return true;
            }
			Debug.Log(" === [BuildingModifier] Processing: " + buildingDef.PrefabID);
			if (BuildingModifierState.StateManager.State.Debug)
				Debug.Log(" === [BuildingModifier] CreateBuildingDef === ");
			PostProcessClass.Process(buildingDef);
			//configTable[config] = buildingDef;
			((Dictionary < IBuildingConfig, BuildingDef > )configTableF.GetValue(__instance))[config] = buildingDef;
			//GameObject gameObject = Object.Instantiate(baseTemplate);
			GameObject gameObject = UnityEngine.Object.Instantiate((GameObject)baseTemplateF.GetValue(__instance));
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			gameObject.GetComponent<KPrefabID>().PrefabTag = buildingDef.Tag;
			gameObject.name = buildingDef.PrefabID + "Template";
			gameObject.GetComponent<Building>().Def = buildingDef;
			gameObject.GetComponent<OccupyArea>().OccupiedCellsOffsets = buildingDef.PlacementOffsets;
			if (buildingDef.Deprecated)
			{
				gameObject.GetComponent<KPrefabID>().AddTag(GameTags.DeprecatedContent);
			}
			config.ConfigureBuildingTemplate(gameObject, buildingDef.Tag);
			if (BuildingModifierState.StateManager.State.Debug)
				Debug.Log(" === [BuildingModifier] ConfigureBuildingTemplate === ");
			PostProcessClass.Process(buildingDef);
			buildingDef.BuildingComplete = BuildingLoader.Instance.CreateBuildingComplete(gameObject, buildingDef);
			if (BuildingModifierState.StateManager.State.Debug)
				Debug.Log(" === [BuildingModifier] CreateBuildingComplete === ");
			PostProcessClass.Process(buildingDef);
            bool flag = true;
			//for (int i = 0; i < NonBuildableBuildings.Length; i++)
			for (int i = 0; i < ((string[])NonBuildableBuildingsF.GetValue(__instance)).Length; i++)
			{
				//if (buildingDef.PrefabID == NonBuildableBuildings[i])
				if (buildingDef.PrefabID == ((string[])NonBuildableBuildingsF.GetValue(__instance))[i])
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				buildingDef.BuildingUnderConstruction = BuildingLoader.Instance.CreateBuildingUnderConstruction(buildingDef);				
				buildingDef.BuildingUnderConstruction.name = BuildingConfigManager.GetUnderConstructionName(buildingDef.BuildingUnderConstruction.name);
				buildingDef.BuildingPreview = BuildingLoader.Instance.CreateBuildingPreview(buildingDef);				
				buildingDef.BuildingPreview.name += "Preview";
			}
			
			buildingDef.PostProcess();
			config.DoPostConfigureComplete(buildingDef.BuildingComplete);
			if (BuildingModifierState.StateManager.State.Debug)
				Debug.Log(" === [BuildingModifier] DoPostConfigureComplete === ");
			PostProcessClass.Process(buildingDef);
			if (flag)
			{
				config.DoPostConfigurePreview(buildingDef, buildingDef.BuildingPreview);
				if (BuildingModifierState.StateManager.State.Debug)
					Debug.Log(" === [BuildingModifier] CreateBuildingUnderConstruction === ");
				PostProcessClass.Process(buildingDef);
				config.DoPostConfigureUnderConstruction(buildingDef.BuildingUnderConstruction);
				if (BuildingModifierState.StateManager.State.Debug)
					Debug.Log(" === [BuildingModifier] CreateBuildingPreview === ");
				PostProcessClass.Process(buildingDef);
			}
			Assets.AddBuildingDef(buildingDef);
			return false;
		}

	}

	
	[HarmonyPatch(typeof(BuildingConfigManager), "ConfigurePost")]
	internal class BuildingModifierMod_BuildingConfigManager_ConfigurePost
	{

		private static void Postfix(BuildingConfigManager __instance)
		{
			if (!BuildingModifierState.StateManager.State.Enabled)
				return;

			if (BuildingModifierState.StateManager.State.Debug)
				Debug.Log(" === [BuildingModifier] BuildingModifierMod_BuildingConfigManager_ConfigurePost Postfix === ");
			
			FieldInfo configTableF = AccessTools.Field(typeof(BuildingConfigManager), "configTable");


			foreach (KeyValuePair<IBuildingConfig, BuildingDef> item in (Dictionary <IBuildingConfig, BuildingDef>)configTableF.GetValue(__instance))
			{
                if (!BuildingModifierState.StateManager.State.Modifiers.ContainsKey(item.Value.PrefabID))
                    continue;

				if (BuildingModifierState.StateManager.State.Debug)
					Debug.Log(" === [BuildingModifier] ConfigurePost === ");
				PostProcessClass.Process(item.Value);
			}

			if (PostProcessClass.ModifiersAll.Count() != PostProcessClass.ModifiersFound.Count())
			{
				Debug.Log(" === [BuildingModifier] Not found modifiers:");
				foreach (string modifier in PostProcessClass.ModifiersAll)
				{
					if (!PostProcessClass.ModifiersFound.Contains(modifier))
					{
						Debug.Log(modifier);
					}
				}
			}
			else {
				Debug.Log(" === [BuildingModifier] Finished.");
			}
		}
	}
	
	internal class PostProcessClass
	{
		
        public static HashSet<string> ModifiersAll = new HashSet<string>();
        public static HashSet<string> ModifiersFound = new HashSet<string>();

        public static void Process(BuildingDef def)
		{
			if (BuildingModifierState.StateManager.State.Debug)
				Debug.Log(" === [BuildingModifier] Process === " + def.PrefabID);
			bool error = false;

			ModifiersAll.Add(def.PrefabID);

			if (ModifiersFound.Contains(def.PrefabID))
				return;

			Dictionary<string, object> entry = BuildingModifierState.StateManager.State.Modifiers[def.PrefabID];
			
			try
				{
					//BuildingDef def = Assets.GetBuildingDef(entry.Key);
					//BuildingDef def = __instance;
					//if (!def.PrefabID.Equals(entry.Key)) continue;

					//Debug.Log(entry.Key);
					//Debug.Log(def);

					foreach (KeyValuePair<string, object> modifier in entry)
					{
						try {
							//Debug.Log(modifier.Key + ": " + modifier.Value.GetType() + "; " + modifier.Value);

                            ModifiersAll.Add(def.PrefabID + "_"+ modifier.Key);

                            Type value = modifier.Value.GetType();
							if (value.Equals(typeof(JObject)))
							{
								try
								{
                                    if (def.BuildingComplete != null)
                                    {
                                        PostProcessClass.ProcessComponent(ref def.BuildingComplete, def.PrefabID, modifier.Key, (JObject)modifier.Value);
                                        Debug.Log(" === [BuildingModifier] Found: " + def.PrefabID + "_" + modifier.Key);
                                        ModifiersFound.Add(def.PrefabID + "_" + modifier.Key);
                                    }
									else
									{
										error = true;
									}
								}
								catch (Exception ex)
								{
									//Debug.LogError(ex);
									error = true;
                                    Debug.Log(" === [BuildingModifier] Warning: " + def.PrefabID + "_" + modifier.Key);
                                }
							}
							else if (value.Equals(typeof(Int64)))
							{
								FieldInfo fi = AccessTools.Field(typeof(BuildingDef), modifier.Key);
								fi.SetValue(def, modifier.Value);
                                Debug.Log(" === [BuildingModifier] Found: " + def.PrefabID + "_" + modifier.Key);
                                ModifiersFound.Add(def.PrefabID + "_" + modifier.Key);
                            }
							else if (value.Equals(typeof(Double)))
							{
								FieldInfo fi = AccessTools.Field(typeof(BuildingDef), modifier.Key);
								fi.SetValue(def, modifier.Value);
                                Debug.Log(" === [BuildingModifier] Found: " + def.PrefabID + "_" + modifier.Key);
                                ModifiersFound.Add(def.PrefabID + "_" + modifier.Key);
                            }
							else if (value.Equals(typeof(String)))
							{
								FieldInfo fi = AccessTools.Field(typeof(BuildingDef), modifier.Key);
								string path = (string)modifier.Value;
								string className = path.Substring(0, path.LastIndexOf("."));
								string fieldName = path.Substring(path.LastIndexOf(".") + 1);
								//Debug.Log(className + ", " + fieldName);
								Type classType = Type.GetType(className + ", Assembly-CSharp");
								//Debug.Log("Type: " + classType);
								FieldInfo fi2 = AccessTools.Field(classType, fieldName);
								//var obj = Activator.CreateInstance(tp);
								//Debug.Log("FINAL: " + fi2.GetValue(null));
								fi.SetValue(def, fi2.GetValue(null));
                                Debug.Log(" === [BuildingModifier] Found: " + def.PrefabID + "_" + modifier.Key);
                                ModifiersFound.Add(def.PrefabID + "_" + modifier.Key);
                            }
							else if (value.Equals(typeof(Boolean)))
							{
								FieldInfo fi = AccessTools.Field(typeof(BuildingDef), modifier.Key);
								fi.SetValue(def, modifier.Value);
                                Debug.Log(" === [BuildingModifier] Found: " + def.PrefabID + "_" + modifier.Key);
                                ModifiersFound.Add(def.PrefabID + "_" + modifier.Key);
                            }

                           
                        }
						catch (Exception ex)
						{
                            //Debug.LogError(ex);
                            Debug.Log(" === [BuildingModifier] Warning: " + def.PrefabID + "_" + modifier.Key);

                        }
					}
					
				}
				catch (Exception ex)
				{
					error = true;
                    //Debug.LogError(ex);
                    Debug.Log(" === [BuildingModifier] Warning: " + def.PrefabID + "_"+ entry);
                }
				//PostProcessClass.PostProcess(ref def.BuildingComplete);

				/*
                var original = Type.GetType(entry.Key+", Assembly-CSharp").GetMethod("DoPostConfigureComplete");
                //var prefix = typeof(MyPatchClass1).GetMethod("SomeMethod");
                //var postfix = typeof(TestClass).GetMethod("Modify");
               
                //harmony.Patch(original, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
                harmony.Patch(original, new HarmonyMethod(null), new HarmonyMethod(postfix));
                */


			if (!error)
			{
				Debug.Log(" === [BuildingModifier] Found: " + def.PrefabID);
				ModifiersFound.Add(def.PrefabID);
			}
		}

		public static void ProcessComponent(ref GameObject go, String buildingName, String componentName, JObject jobj)
		{
			Debug.Log(" === [BuildingModifier] ProcessComponent === " + go.PrefabID().Name);

			foreach (JProperty x in (JToken)jobj)
			{ 
				// if 'obj' is a JObject
			
				string name = x.Name;
				JToken value = x.Value;
                 try
                 {
                    //Debug.Log(componentName + ", " + name + ": " + value.ToString());

                    if (ModifiersFound.Contains(buildingName + "_" + componentName + "_" + name))
                        continue;

                    ModifiersAll.Add(buildingName + "_" + componentName + "_" + name);

                    MethodInfo method = typeof(GameObject).GetMethod("GetComponent", new Type[] { typeof(Type) });
					var component = method.Invoke(go, new object[] { Type.GetType(componentName + ", Assembly-CSharp") });
					FieldInfo fi = AccessTools.Field(Type.GetType(componentName + ", Assembly-CSharp"), name);
					//Debug.Log(value + " " + value.Type);
					switch (value.Type)
					{
						case JTokenType.Integer:
							fi.SetValue(component, (int)value);
							break;
						case JTokenType.Float:
							fi.SetValue(component, (float)value);
							break;
						case JTokenType.Boolean:
							fi.SetValue(component, (bool)value);
							break;
						case JTokenType.String:
							//fi.SetValue(component, (string)value);
							break;
						case JTokenType.Object:
							//fi.SetValue(component, (string)value);
							break;
						default:
							break;
					}
                    Debug.Log(" === [BuildingModifier] Found: " + buildingName + "_" + componentName + "_" + name);
                    ModifiersFound.Add(buildingName + "_" + componentName + "_" + name);
                }
				catch (Exception ex)
				{
                    //Debug.LogError(ex);
                    Debug.Log(" === [BuildingModifier] Warning: " + buildingName + "_" + componentName + "_" + name);
                    throw ex;
                }
			}

		}
		/*
		public static void PostProcessOld(ref GameObject go)
		{
			Debug.Log(" === PostProcess === " + go.PrefabID().Name);
			//Storage storage = go.AddOrGet<Storage>();
			//storage.capacityKg = 10000f;

			//var test = BuildingModifierState.StateManager.State.Modifiers[go.PrefabID];

			// MethodInfo method = typeof(EntityTemplateExtensions).GetMethod("AddOrGet");
			MethodInfo method = typeof(GameObject).GetMethod("GetComponent", new Type[] { typeof(Type) });

			Debug.Log("a" + method);
			//MethodInfo generic = method.MakeGenericMethod(Type.GetType("Storage, Assembly-CSharp"));
			//Debug.Log("b"+ generic);
			var component = method.Invoke(go, new object[] { Type.GetType("Storage, Assembly-CSharp") });
			Debug.Log("c" + component);
			FieldInfo fi = AccessTools.Field(Type.GetType("Storage, Assembly-CSharp"), "capacityKg");
			Debug.Log("d" + fi);
			fi.SetValue(component, 10000f);
			Debug.Log("e");
		}
		*/

	}

	/*
    [HarmonyPatch(typeof(BuildingDef), "PostProcess")]
    internal class BuildingModifierMod_BuildingDef_PostProcess
    {


        private static void Prefix(BuildingDef __instance)
        {
            //Debug.Log(" === BuildingModifierMod_BuildingDef_PostProcess Prefix === ");
            
            //var harmony = HarmonyInstance.Create("Assembly-CSharp");
            //harmony.PatchAll(Assembly.GetExecutingAssembly());
            //var harmony = HarmonyInstance.Create("ONI-Modloader");
            var harmony = HarmonyInstance.Create("Assembly-CSharp");
            //harmony.PatchAll(Assembly.GetExecutingAssembly());
            var methods = harmony.GetPatchedMethods();
            foreach (var method in methods)
            {
                Debug.Log(method.ToString());
            }
            //harmony.PatchAll(Assembly.LoadFrom("Assembly-CSharp"));

            //MethodInfo postfix = AccessTools.Method(typeof(PostProcessClass), "PostProcess");

            foreach (var method in Assets.BuildingDefs)
            {
                Debug.Log(method.PrefabID);
            }
            

            //PostProcessClass.Dummy(__instance);

        }

    }
	*/

	/*
	[HarmonyPatch(typeof(IBuildingConfig), "CreateBuildingDef")]
	internal class BuildingModifierMod_IBuildingConfig_CreateBuildingDef
	{

		private static void Postfix(BuildingDef __result)
		{
			
			PostProcessClass.Dummy(__result);
		}
	}

	[HarmonyPatch(typeof(IBuildingConfig), "ConfigureBuildingTemplate")]
	internal class BuildingModifierMod_IBuildingConfig_ConfigureBuildingTemplate
	{

		private static void Postfix(GameObject go, Tag prefab_tag)
		{
			PostProcessClass.Dummy(go.GetComponent<Building>().Def);
		}
	}

	[HarmonyPatch(typeof(IBuildingConfig), "DoPostConfigureUnderConstruction")]
	internal class BuildingModifierMod_IBuildingConfig_DoPostConfigureUnderConstruction
	{

		private static void Postfix(GameObject go)
		{
			PostProcessClass.Dummy(go.GetComponent<Building>().Def);
		}
	}

	[HarmonyPatch(typeof(IBuildingConfig), "DoPostConfigurePreview")]
	internal class BuildingModifierMod_IBuildingConfig_DoPostConfigurePreview
	{

		private static void Postfix(BuildingDef def, GameObject go)
		{
			PostProcessClass.Dummy(def);
		}
	}

	[HarmonyPatch(typeof(IBuildingConfig), "DoPostConfigureComplete")]
	internal class BuildingModifierMod_IBuildingConfig_DoPostConfigureComplete
	{

		private static void Postfix(GameObject go)
		{
			PostProcessClass.Dummy(go.GetComponent<Building>().Def);
		}
	}

	[HarmonyPatch(typeof(IBuildingConfig), "ConfigurePost")]
	internal class BuildingModifierMod_IBuildingConfig_ConfigurePost
	{

		private static void Postfix(BuildingDef def)
		{
			PostProcessClass.Dummy(def);
		}
	}
	*/

}
