using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;

namespace NB_ModPack_Core
{
    public class TerrainUpdateDef : BuildableUpdateDef
    {
        #region XML Data
        public List<TerrainDef> thingDefs;

        public string holdSnow;

        #endregion

        [Unsaved]

        #region Instance Data
        protected bool holdSnowBool;

        protected override List<T> GetDefs<T>()
        {
            return thingDefs.Cast<T>().ToList();
        }

        protected override string GetErrors()
        {
            var errors = base.GetErrors();

            if (!holdSnow.NullOrEmpty() && !bool.TryParse(holdSnow, out holdSnowBool))
            {
                errors += "\n\tholdSnow must be a bool";
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

            var setHoldSnow = !holdSnow.NullOrEmpty();

            foreach (var targetDef in thingDefs)
            {
                if (setHoldSnow)
                {
                    targetDef.holdSnow = holdSnowBool;
                }
            }

            return true;
        }

        #endregion
    }

}
