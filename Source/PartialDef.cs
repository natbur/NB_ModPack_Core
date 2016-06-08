using System;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;

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

        [Unsaved]
        private Type targetType;

        [Unsaved]
        private MethodInfo getNamed;

        #region Helpers
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

        #endregion

        #region XmlLoading
        
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

                XmlHelper.ResolveOptionalNodes(xmlRoot, allDefNames);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                // Log.Error(ex.StackTrace);
                return;
            }
        }

        #endregion

        #region Def updating
        public void UpdateTargetDefs()
        {
            // Log.Message("Custom XML Loader invoked\n");

            // Remove comments so we don't have to worry about them later
            XmlHelper.StripComments(xmlRoot);

            // Get the type of Def that we're updating
            targetType = XmlHelper.GetRefType(xmlRoot);

            // Process all optional attributes
            HandleOptionalNodes(xmlRoot);
            if (!xmlRoot.HasChildNodes)
            {
                // Root was purged during pre-parse
                // Abort all updates
                return;
            }

            XmlNode targetsNode = xmlRoot.SelectSingleNode("targetDefs");
            XmlNode propertiesNode = xmlRoot.SelectSingleNode("properties");

            // Call RimWorlds parser to load in our properties node
            Def parsed = LoadProperties(propertiesNode);

            // Get a handle on all target defs
            List<Def> targets = LoadTargetDefs(targetsNode);

            // Update properties on target defs, raw xml used for update info
            UpdateFields(targets, parsed, propertiesNode);

            // We've done our damage, unload ourselves from RimWorld
            GenGeneric.InvokeStaticMethodOnGenericType(typeof(DefDatabase<>), typeof(PartialDef), "Remove", this);

            // Log.Message(string.Format("Parsed Xml: {0}", xmlRoot.OuterXml));
        }

        private void UpdateFields(IEnumerable<Def> targets, Def source, XmlNode properties)
        {
            // Loop through all the fields on the parsed(source) Def
            foreach (FieldInfo sourceField in source.GetType().GetFields(GenGeneric.BindingFlagsAll))
            {
                // Check if this node should be updated, and get append mode for lists/dicts
                bool append;
                if (ShouldUpdate(sourceField, properties, out append))
                {
                    // Update all of the targets
                    foreach (Def target in targets)
                    {
                        // RW doesn't care if it's private/public, so neither do we
                        FieldInfo targetField = target.GetType().GetField(sourceField.Name, GenGeneric.BindingFlagsAll | BindingFlags.IgnoreCase);

                        // Sanity checks
                        if (targetField == null)
                        {
                            Log.Error(string.Format("Target {0} on {1} doesn't exist", targetField.Name, target.defName));
                            continue;
                        }
                        else if (sourceField.FieldType != targetField.FieldType)
                        {
                            Log.Error(string.Format("Source and target field types differ {1}: {2}", sourceField.Name, targetField.Name));
                            continue;
                        }

                        if (append)
                        {
                            // Both Lists and Dicts can be appended, so figure out which we're dealing with
                            if (typeof(System.Collections.IList).IsAssignableFrom(sourceField.FieldType))
                            {
                                var sourceList = (System.Collections.IList)sourceField.GetValue(source);
                                var targetList = (System.Collections.IList)targetField.GetValue(target);

                                // Constructors for Defs don't initialize lists
                                if (targetList == null)
                                {
                                    targetField.SetValue(target, Activator.CreateInstance(targetField.FieldType));
                                    targetList = (System.Collections.IList)targetField.GetValue(target);
                                }

                                foreach (var item in sourceList)
                                {
                                    targetList.Add(item);
                                }
                            }
                            else if (typeof(System.Collections.IDictionary).IsAssignableFrom(sourceField.FieldType))
                            {
                                var sourceDict = (System.Collections.IDictionary)sourceField.GetValue(source);
                                var targetDict = (System.Collections.IDictionary)targetField.GetValue(target);

                                // Constructors for Defs don't initialize dicts
                                if (targetDict == null)
                                {
                                    targetField.SetValue(target, Activator.CreateInstance(targetField.FieldType));
                                    targetDict = (System.Collections.IDictionary)targetField.GetValue(target);
                                }

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
                            // Overwrite existing property value
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
            // If the field doesn't exist in the processed xml, don't update
            if (propNode == null)
            {
                return false;
            }

            // Otherwise, check if it's a collection and we should append the new values
            append = propNode.GetAttributeValue("mode") == "append" && typeof(System.Collections.ICollection).IsAssignableFrom(targetField.FieldType);

            return true;
        }
        #endregion

        #region Overrides
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

            this.xmlRoot = xmlRoot;
        }

        public override void PostLoad()
        {
            base.PostLoad();
            LongEventHandler.ExecuteWhenFinished(UpdateTargetDefs);
        }

        public override void ResolveReferences()
        {
            base.ResolveReferences();
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
