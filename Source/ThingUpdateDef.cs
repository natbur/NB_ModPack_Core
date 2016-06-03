using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;

namespace NB_ModPack_Core
{

    public class ThingUpdateDef : BuildableUpdateDef
    {
        #region XML Data
        public List<ThingDef> thingDefs;

        public List<StuffCategoryDef> stuffCategories;

        #endregion

        [Unsaved]
        public bool dummy = false;

        #region Instance Data

        protected override List<T> GetDefs<T>()
        {
            return thingDefs.Cast<T>().ToList();
        }

        protected override string GetErrors()
        {
            var errors = base.GetErrors();

            if (thingDefs.NullOrEmpty())
            {
                errors += "\n\tMissing thingDefs";
            }

            if (!costStuffCount.NullOrEmpty())
            {
                if (stuffCategories != null && stuffCategories.Count == 0)
                {
                    errors += string.Format("\n\tCannot set costStuffCount with empty stuffCategories");
                }
                else
                {
                    foreach (ThingDef thingDef in thingDefs)
                    {
                        if (thingDef.stuffCategories.NullOrEmpty())
                        {
                            errors += string.Format("\n\tCannot set costStuffCount on {0}, no stuffCategories", thingDef.defName);
                        }
                    }
                }

            }
            
            return errors;
        }

        #endregion

        #region Process State
        
        public override bool UpdateThings()
        {
            if (!base.UpdateThings())
            {
                return false;
            }

            var setStuffCategories = stuffCategories != null;

            foreach (var targetDef in thingDefs)
            {
                if (setStuffCategories)
                {
                    targetDef.stuffCategories = stuffCategories;
                }
            }

            return true;

        }

        #endregion
    }

}
