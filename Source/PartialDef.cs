using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using RimWorld;
using Verse;

namespace NB_ModPack_Core
{
    /*
    <!-- Only one category of Defs per partial (Possibly allow for base type i.e. BuildableDef -->
    <partialDef type="DEFTYPE" priority="INT"/>

    <!-- If a Def exists, process tag -->
    <TAG ifExists=""/>

    <!-- If a Def doesn't exist, process tag -->
    <TAG ifNExists=""/>

    <!-- Try to process tag, ignore errors -->
    <TAG optional/>

    <!-- If a mod exists, process tag -->
    <TAG ifModExists=""/>

    <!-- If a mod doesn't exist, process tag -->
    <TAG ifModNExists=""/>

    <!-- Append or replace items in list (non-unique always replace), default replace -->
    <LISTTAG mode="replace|append"/>
    */

    /*
      <PartialDef type="ThingDef" priority="0">
        <defName>NB_bedUpdate</defName>
        <targetDefs>
          <!-- Always target bed, target FancySingleBed if it exists -->
          <li>Bed</li>
          <li ifExists="TableStonecutter">TableStonecutter</li>
          <li optional="">FancySingleBed</li>
        </targetDefs>
        <!-- Replace existing cost list -->
        <properties>
          <costList>
            <!-- Only add WovenCloth if it exists, otherwise add regular cloth -->
            <WovenCloth optional="">12</WovenCloth>
            <Cloth optional="WovenCloth">12</Cloth>
          </costList>
          <!-- Append new researches to any already loaded -->
          <researchPrerequisites mode="append">
            <!-- Three ways of doing the same thing -->
            <li ifModExists="NB_ClothWeaving">ClothMaking</li>
            <li ifExists="WovenCloth">ClothMaking</li>
            <li optional="">ClothMaking</li>
            <li>CarpetMaking</li>
          </researchPrerequisites>
        </properties>
      </PartialDef>

    <partialDef type="ThingDef" priority="9" ifModNExists="FluffysBetterMod">
    ...
    </partialDef>
    */

    public class PartialDef : Def
    {
        [Unsaved]
        private XmlNode xmlRoot;
        private Type targetType;
        private MethodInfo getNamed;

        #region Helpers
        public static Type GetRefType(XmlNode xmlRoot, string attributeName = "type", bool checkDef = true)
        {
            XmlAttribute xmlAttribute = xmlRoot.Attributes[attributeName];
            if (xmlAttribute == null)
            {
                return null;
            }

            Type T = GenTypes.GetTypeInAnyAssembly(xmlAttribute.Value);
            if (T == null)
            {
                Log.Error("Could not find type named " + xmlAttribute.Value + " from node " + xmlRoot.OuterXml);
                return null;
            }

            if (checkDef && (!T.IsSubclassOf(typeof(Def)) && T != typeof(Def)))
            {
                return null;
            }

            return T;
        }

        private Def GetNamedSilentFail(string name)
        {
            if (this.getNamed == null)
            {
                Type genDB = typeof(DefDatabase<>);
                Type typedDB = genDB.MakeGenericType(targetType);
                getNamed = typedDB.GetMethod("GetNamedSilentFail", BindingFlags.Static | BindingFlags.Public);
            }

            return (Def)getNamed.Invoke(null, new object[] { name });
        }

        public void StripComments(XmlNode xmlRoot)
        {
            XmlNodeList nodes = xmlRoot.SelectNodes("//comment()");
            foreach (XmlNode node in nodes.Cast<XmlNode>().ToArray())
            {
                node.ParentNode.RemoveChild(node);
            }
        }
        #endregion

        #region XmlLoading

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            // Abuse the DEF system a little bit
            // since we don't actually need to load anything here
            try
            {
                defName = xmlRoot.SelectSingleNode("defName").InnerText;
            }
            catch
            {
                // Since we unload after we're done, we don't care if we don't have a defname
                defName = Guid.NewGuid().ToString();
            }

            // Log.Message("Custom XML Loader invoked\n");
            this.xmlRoot = xmlRoot;
            targetType = GetRefType(xmlRoot);
        }

        public Def LoadProperties(XmlNode properties)
        {
            try
            {
                Def parsed = (Def)GenGeneric.InvokeStaticGenericMethod(
                    typeof(XmlToObject),
                    targetType,
                    "ObjectFromXml",
                    properties,
                    false);

                // Log.Warning(string.Format("Loaded {0} Type: {1}", parsed.defName, parsed.GetType().Name));
                return parsed;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                // Log.Error(ex.StackTrace);
                return null;
            }
        }

        public List<Def> LoadTargetDefs(XmlNode targetDefs)
        {
            try
            {
                List<Def> defs = new List<Def>();
                foreach (XmlNode node in targetDefs.ChildNodes)
                {
                    // Log.Message(string.Format("Loading Def {0}", node.InnerText));
                    Def def = GetNamedSilentFail(node.InnerText);
                    if (def != null)
                    {
                        // Log.Message(string.Format("Loaded Def {0}", node.InnerText));
                        defs.Add(def);
                    }
                }

                CrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);
                // Log.Warning(string.Format("Loaded {0} targets.", defs.Count));
                return defs;
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                // Log.Error(ex.StackTrace);
                return null;
            }
        }
        #endregion

        #region Optional Node Resolution

        public void HandleOptionalNodes(XmlNode xmlRoot)
        {
            try
            {
                ICollection<string> allDefNames = new HashSet<string>();
                foreach (Type defType in typeof(Def).AllSubclasses())
                {
                    var genType = typeof(DefDatabase<>).MakeGenericType(new Type[] { defType });
                    foreach (var def in (System.Collections.IList)genType.GetProperty("AllDefs").GetValue(null, null))
                    {
                        allDefNames.Add(((Def)def).defName);
                    }
                }

                ResolveOptionalNodes(xmlRoot, allDefNames);
                ResolveIFModNodes(xmlRoot, allDefNames);
                ResolveIFNodes(xmlRoot, allDefNames);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                // Log.Error(ex.StackTrace);
                return;
            }
        }

        public void ResolveNodes(XmlNode xmlRoot, string selector, string removeAttribute, Predicate<XmlNode> Keep)
        {
            XmlNodeList nodes = xmlRoot.SelectNodes(selector);

            foreach (XmlNode node in nodes)
            {
                if (Keep(node))
                {
                    if (!string.IsNullOrEmpty(removeAttribute))
                    {
                        node.Attributes.Remove(node.Attributes[removeAttribute]);
                    }
                }
                else
                {
                    if (node == xmlRoot)
                    {
                        xmlRoot.RemoveAll();
                        break;
                    }

                    node.ParentNode.RemoveChild(node);
                }
            }
        }

        public void ResolveOptionalNodes(XmlNode xmlRoot, ICollection<string> AllDefNames)
        {
            //< !--Try to process tag, ignore errors -->
            // < li optional />
            // <WoolCloth optional />
            ResolveNodes(xmlRoot,
                "//*[@optional]",
                "optional",
                n => AllDefNames.Contains(n.InnerText) || (n.Name != "li" && AllDefNames.Contains(n.Name)));
        }

        public void ResolveIFNodes(XmlNode xmlRoot, ICollection<string> AllDefNames)
        {
            //< !--If a Def exists, process tag -->
            //< TAG ifExists = "" />
            ResolveNodes(xmlRoot,
                "//*[@ifExists]",
                "ifExists",
                n => AllDefNames.Contains(n.Attributes["ifExists"].Value));

            // < !--If a Def doesn't exist, process tag -->
            // < TAG ifNExists = "" />
            ResolveNodes(xmlRoot,
                "//*[@ifNExists]",
                "ifNExists",
                n => !AllDefNames.Contains(n.Attributes["ifNExists"].Value));
        }

        public void ResolveIFModNodes(XmlNode xmlRoot, ICollection<string> AllDefNames)
        {
            //< !--If a mod exists, process tag -->
            //< TAG ifModExists = "" />
            ResolveNodes(xmlRoot,
                "//*[@ifModExists]",
                "ifModExists",
                n => LoadedModManager.LoadedMods.SingleOrDefault(m => m.name == n.Attributes["ifModExists"].Value) != null);

            // < !--If a mod doesn't exist, process tag -->
            // < TAG ifModNExists = "" />
            ResolveNodes(xmlRoot,
                "//*[@ifModNexists]",
                "ifModNexists",
                n => LoadedModManager.LoadedMods.SingleOrDefault(m => m.name == n.Attributes["ifModNexists"].Value) == null);
        }
        #endregion

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            // **********************************************************************
            // Not exacly resolving references here, but this is the current callback
            // avaialbe that occurs after all defs have been loaded
            // If a hook were avialable between LoadAllDefs and CrossRef.Resolve, 
            // that would be a better spot
            // **********************************************************************

            // Remove comments so we don't have to worry about them later
            StripComments(xmlRoot);

            // Process all optiona/if attributes
            HandleOptionalNodes(xmlRoot);
            if(!xmlRoot.HasChildNodes)
            {
                // Root failed a check and was removed
                return;
            }

            XmlNode targetsNode = xmlRoot.SelectSingleNode("targetDefs");
            XmlNode propertiesNode = xmlRoot.SelectSingleNode("properties");

            // Call RimWorlds parser to load in our properties node
            Def parsed = LoadProperties(propertiesNode);

            // Get a handle on all target defs
            List<Def> targets = LoadTargetDefs(targetsNode);

            // Update properties on target defs, raw xml used for update info
            UpdateProperties(targets, parsed, propertiesNode);

            // We've done our damage, unload ourselves from RimWorld
            GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), typeof(PartialDef), "Remove", this);

            // Log.Message(string.Format("Parsed Xml: {0}", xmlRoot.OuterXml));
        }

        private void UpdateProperties(IEnumerable<Def> targets, Def source, XmlNode properties)
        {
            foreach (FieldInfo sourceField in source.GetType().GetFields(GenGeneric.BindingFlagsAll))
            {
                bool append;
                if (ShouldUpdate(sourceField, properties, out append))
                {
                    foreach (Def target in targets)
                    {
                        FieldInfo targetField = target.GetType().GetField(sourceField.Name, GenGeneric.BindingFlagsAll);
                        if (targetField == null)
                        {
                            Log.Error(string.Format("Target {0} on {1} doesn't exist", targetField.Name, target.defName));
                            continue;
                        }

                        if (sourceField.FieldType != targetField.FieldType)
                        {
                            Log.Error(string.Format("Source and target field types differ {1}: {2}", sourceField.Name, targetField.Name));
                            continue;
                        }

                        if (append)
                        {
                            // Log.Message(string.Format("Appending to field {0}", targetField.Name));
                            if (typeof(System.Collections.IList).IsAssignableFrom(sourceField.FieldType))
                            {
                                var sourceList = (System.Collections.IList)sourceField.GetValue(source);
                                var targetList = (System.Collections.IList)targetField.GetValue(target);

                                if (targetList == null)
                                    targetField.SetValue(target, Activator.CreateInstance(targetField.FieldType));
                                targetList = (System.Collections.IList)targetField.GetValue(target);

                                foreach (var item in sourceList)
                                {
                                    targetList.Add(item);
                                }
                            }
                            else if (typeof(System.Collections.IDictionary).IsAssignableFrom(sourceField.FieldType))
                            {
                                var sourceDict = (System.Collections.IDictionary)sourceField.GetValue(source);
                                var targetDict = (System.Collections.IDictionary)targetField.GetValue(target);
                                if (targetDict == null)
                                    targetField.SetValue(target, Activator.CreateInstance(targetField.FieldType));
                                targetDict = (System.Collections.IDictionary)targetField.GetValue(target);

                                foreach (var key in sourceDict.Keys)
                                {
                                    targetDict[key] = sourceDict[key];
                                }
                            }
                            else
                            {
                                Log.Warning(string.Format("Unknown appendable type: {0}", sourceField.FieldType.Name));
                                continue;
                            }
                        }
                        else
                        {
                            try
                            {
                                targetField.SetValue(target, sourceField.GetValue(source));
                            }
                            catch (Exception ex)
                            {
                                Log.Error(string.Format("Failed to assign {0} on {1}: {2}", targetField.Name, target.defName, ex.Message));
                            }
                        }
                    }
                }
            }
        }

        private bool ShouldUpdate(FieldInfo targetField, XmlNode properties, out bool append)
        {
            append = false;
            XmlNode propNode = properties.SelectSingleNode(targetField.Name);
            if (propNode == null)
            {
                return false;
            }
            
            if (propNode.Attributes["mode"] != null && propNode.Attributes["mode"].Value == "append")
            {
                if (typeof(System.Collections.IList).IsAssignableFrom(targetField.FieldType) || typeof(System.Collections.IDictionary).IsAssignableFrom(targetField.FieldType))
                {
                    append = true;
                }
            }

            return true;
        }

        #region Other Overrides
        public override void PostLoad()
        {
            base.PostLoad();
        }

        public override IEnumerable<string> ConfigErrors()
        {
            return base.ConfigErrors();
        }

        public override void ClearCachedData()
        {
            base.ClearCachedData();
        }
        #endregion
    }
}
