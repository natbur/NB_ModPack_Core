using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;

namespace NB_ModPack_Core
{

    public class RecipeUpdateDef : UpdateDef
    {
        #region XML Data
        public List<RecipeDef> thingDefs;

        // These are optionally defined in xml
        public ThingFilter fixedIngredientFilter;
        public List<IngredientCount> ingredients;
        public List<ThingCount> products;
        public List<ThingDef> recipeUsers;

        // Resarch prereqs
        public ResearchProjectDef researchPrerequisite;
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

            var setIngredientFilter = fixedIngredientFilter != null;
            var setIngredients = ingredients != null;
            var setProducts = products != null;
            var setRecipeUsers = recipeUsers != null;
            var setResearch = researchPrerequisite != null;

            foreach (var def in thingDefs)
            {
                var recipeDef = (RecipeDef)def;
                if (setIngredientFilter)
                {
                    recipeDef.fixedIngredientFilter = fixedIngredientFilter;
                }

                if (setIngredients)
                {
                    recipeDef.ingredients = ingredients;
                }

                if (setProducts)
                {
                    recipeDef.products = products;
                }
                if (setRecipeUsers)
                {
                    recipeDef.recipeUsers = recipeUsers;
                }
                if (setResearch)
                {
                    recipeDef.researchPrerequisite = researchPrerequisite;
                }
            }

            return true;

        }

        #endregion
    }

}
