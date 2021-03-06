using System;
using System.Collections.Generic;
using System.Text;
using DevExpress;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using CDTDatabase;
using CDTLib;
using Plugins;
using System.Data;
using DevExpress.XtraGrid.Views.Grid;

namespace SuaTenHV
{
    public class SuaTenHV:ICData
    {
        private InfoCustomData _info;
        private DataCustomData _data;     
        public SuaTenHV()
        {
            _info = new InfoCustomData(IDataType.MasterDetailDt);
        }

       
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
            update();
            
        }

        private void insertQTHT()
        {
            
        }

        private void update()
        {
            if (_data.CurMasterIndex < 0)
                return;
            DataRow row = _data.DsData.Tables[0].Rows[_data.CurMasterIndex];

            if(row.RowState != DataRowState.Modified)
                return;

            //Thay đổi tên học viên
            if (row["TenHV", DataRowVersion.Original].ToString() != row["TenHV", DataRowVersion.Current].ToString())
            {
                string hvtvid = row["HVTVID"].ToString();
                string mahv = row["MaHV"].ToString();
                string tenhv = row["TenHV"].ToString();
                ChangeName(hvtvid, mahv, tenhv);
            }
        }

        private void ChangeName(string hvtvid, string mahv, string tenhv)
        {            
            // HV tư vấn, hv đăng ký, dm khách hàng, hv chuyển lớp, phiếu thu, phiếu chi, blvt, bltk.
            //MTDK
            string sql = sql = "Update MTDK set TenHV = N'" + tenhv + "' where HVTVID = '" + hvtvid + "'";
            _data.DbData.UpdateByNonQuery(sql);
            sql = "Update DMKH set TenKH = N'" + tenhv + "' where MaKH = '" + mahv + "'";
            _data.DbData.UpdateByNonQuery(sql);
            sql = "Update MTChuyenLop set TenHV = N'" + tenhv + "' where TVID = '" + hvtvid + "'";
            _data.DbData.UpdateByNonQuery(sql);
            sql = "Update MT11 set TenKH = N'" + tenhv + "' where MaKH = '" + mahv + "'";
            _data.DbData.UpdateByNonQuery(sql);
            sql = "Update DT11 set TenKHCt = N'" + tenhv + "' where MaKhCt = '" + mahv + "'";
            _data.DbData.UpdateByNonQuery(sql);
            sql = "Update MT12 set TenKH = N'" + tenhv + "' where MaKH = '" + mahv + "'";
            _data.DbData.UpdateByNonQuery(sql);
            sql = "Update DT12 set TenKHCt = N'" + tenhv + "' where MaKhCt = '" + mahv + "'";
            _data.DbData.UpdateByNonQuery(sql);
            sql = "Update BLVT set TenKH = N'" + tenhv + "' where MaKH = '" + mahv + "'";
            _data.DbData.UpdateByNonQuery(sql);
            sql = "Update BLTK set TenKH = N'" + tenhv + "' where MaKH = '" + mahv + "'";
            _data.DbData.UpdateByNonQuery(sql);
            sql = "Update DMKQ set TenHV = N'" + tenhv + "' where HVTVID = '" + hvtvid + "'";
            _data.DbData.UpdateByNonQuery(sql);
            sql = "Update MT32 set TenKH = N'" + tenhv + "' where MaKH = '" + mahv + "'";
            _data.DbData.UpdateByNonQuery(sql);
        }

        public InfoCustomData Info
        {
            get { return _info; }
        }

        #endregion
    }
}
