using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

using Verse;

namespace NB_ModPack_Core
{
    public static class XmlHelper
    {
        public static IDictionary<string, Func<XmlNode, ICollection<string>, bool>> OptionalTags = new Dictionary<string, Func<XmlNode, ICollection<string>, bool>>
        {
            { "optional", delegate(XmlNode n, ICollection<string> defs) {return defs.Contains(n.InnerText) || defs.Contains(n.Name); } },
            { "ifExists", delegate(XmlNode n, ICollection<string> defs) {return defs.Contains(n.GetAttributeValue("ifExists")); } },
            { "ifNExists", delegate(XmlNode n, ICollection<string> defs) {return !defs.Contains(n.GetAttributeValue("ifNExists")); } },
            { "ifModExists", delegate(XmlNode n, ICollection<string> defs) {return LoadedModManager.LoadedMods.SingleOrDefault(m => m.name == n.GetAttributeValue("ifModExists")) != null; } },
            { "ifModNexists", delegate(XmlNode n, ICollection<string> defs) {return LoadedModManager.LoadedMods.SingleOrDefault(m => m.name == n.GetAttributeValue("ifModNexists")) == null; } },
        };

        public static string GetAttributeValue(this XmlNode node, string attribute, string defaultValue = null)
        {
            if (node.Attributes[attribute] != null)
            {
                return node.Attributes[attribute].Value;
            }
            else
            {
                return defaultValue;
            }
        }

        public static Type GetRefType(XmlNode xmlRoot, string attributeName = "type", Type checkType = null)
        {
            string typeName = xmlRoot.GetAttributeValue("type");
            if (typeName == null)
            {
                return null;
            }

            Type T = GenTypes.GetTypeInAnyAssembly(typeName);
            if (T == null)
            {
                Log.Error(string.Format("Could not find type named {0} from node {1}", typeName, xmlRoot.OuterXml));
                return null;
            }

            if (checkType != null && !checkType.IsAssignableFrom(T))
            {
                return null;
            }

            return T;
        }

        public static void StripComments(XmlNode xmlRoot)
        {
            XmlNodeList nodes = xmlRoot.SelectNodes("//comment()");
            foreach (XmlNode node in nodes.Cast<XmlNode>().ToArray())
            {
                node.ParentNode.RemoveChild(node);
            }
        }

        public static void ResolveNodes(XmlNode xmlRoot, string selector, string removeAttribute, Predicate<XmlNode> Keep)
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

        public static void ResolveOptionalNodes(XmlNode xmlRoot, ICollection<string> AllDefNames)
        {
            foreach (var item in OptionalTags)
            {
                ResolveNodes(xmlRoot,
                    string.Format("//*[@{0}]", item.Key),
                    item.Key,
                    n => item.Value(n, AllDefNames));
            }
        }
    }
}
