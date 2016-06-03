using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;

namespace NB_ModPack_Core
{
    public abstract class UpdateDef : Def
    {
        #region XML Data

        // Processing priority so everything happens in the order it should
        // Lower value is higher priority
        // TODO: Replicate CCL Priority sorting
        public int Priority;
        
        #endregion

        [Unsaved]

        #region Instance Data

        private string baseMsg;
        protected bool validChecked = false;
        protected bool isValid = false;

        public virtual bool IsValid
        {
            get
            {
                if(!validChecked)
                {
                    var errors = GetErrors();
                    isValid = errors.NullOrEmpty();
                    if (!isValid)
                    {
                        Verse.Log.Error(BaseMsg + errors);
                    }

                    validChecked = true;
                }

                return isValid;
            }
        }

        // Can't use generics in the classDef, throws an error on load.
        // Work around it by defining the list in the concrete class
        // and casting for the base classes
        protected abstract List<T> GetDefs<T>() where T : Def;

        protected virtual string BaseMsg
        {
            get
            {
                if(baseMsg.NullOrEmpty())
                {
                    baseMsg = string.Format("{0} :: {1}", this.GetType().Name, defName);
                }

                return BaseMsg;
            }
        }

        protected virtual string GetErrors()
        {
            var errors = "";

            if (GetDefs<Def>().NullOrEmpty())
            {
                errors += "\n\tMissing thingDefs";
            }

            return errors;
        }

        #endregion

        #region Process State

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            UpdateThings();
        }

        public override void PostLoad()
        {
            base.PostLoad();
        }

        public virtual bool UpdateThings()
        {
            if (!IsValid)
            {
                Verse.Log.Error(BaseMsg + "\n\tInvalid");
                return false;
            }

            return true;
        }

        #endregion
    }

}
