using System;
using System.Collections.Generic;
using System.Text;
using Plugins;

namespace LuuDiemDanh
{
    public class LuuDiemDanh:ICData
    {
        private DataCustomData _data;
        private InfoCustomData _info = new InfoCustomData(IDataType.MasterDetailDt);
        #region ICData Members

        public DataCustomData Data
        {
            set { _data = value; }
        }

        public void ExecuteAfter()
        {
            
        }

        public void ExecuteBefore()
        {
            
        }

        public InfoCustomData Info
        {
            get { return _info; }
        }

        #endregion
    }
}
