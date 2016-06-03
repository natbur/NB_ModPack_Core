using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;

namespace NB_ModPack_Core
{
    public abstract class BuildableUpdateDef : UpdateDef
    {
        #region XML Data

        // These are optionally defined in xml
        public List<ThingCount> costList;
        public string costStuffCount;
        public string menuHidden;

        public List<StatModifier> statBases;

        // Resarch prereqs
        public List<ResearchProjectDef> researchPrerequisites;

        #endregion

        [Unsaved]

        #region Instance Data

        protected bool menuHiddenBool;
        protected int costStuffCountInt;

        protected override string GetErrors()
        {
            var errors = base.GetErrors();
            
            if (!menuHidden.NullOrEmpty() && !bool.TryParse(menuHidden, out menuHiddenBool))
            {
                errors += "\n\tmenuHidden must be a bool";
            }

            if (!costStuffCount.NullOrEmpty() && !int.TryParse(costStuffCount, out costStuffCountInt))
            {
                errors += "\n\tcostStuffCount must be an int";
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

            var setCostList = costList != null;
            var setStuffCount = !costStuffCount.NullOrEmpty();
            var setMenuHidden = !menuHidden.NullOrEmpty();
            var setResearch = researchPrerequisites != null;
            var setStats = statBases != null;

            foreach (var targetDef in GetDefs<BuildableDef>())
            {
                if (setCostList)
                {
                    targetDef.costList = costList;
                }

                if (setStuffCount)
                {
                    targetDef.costStuffCount = costStuffCountInt;
                }
                if (setMenuHidden)
                {
                    targetDef.menuHidden = menuHiddenBool;
                }
                if (setResearch)
                {
                    targetDef.researchPrerequisites = researchPrerequisites;
                }
                if(setStats)
                {
                    foreach(var statMod in statBases)
                    {
                        var curStat = targetDef.statBases.Find(s => s.stat == statMod.stat);
                        if(curStat != null)
                        {
                            targetDef.statBases.Remove(curStat);
                        }
                        targetDef.statBases.Add(statMod);
                    }
                }
            }

            return true;

        }

        #endregion
    }

}
