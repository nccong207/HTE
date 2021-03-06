using System;
using System.Collections.Generic;
using System.Text;
using DevExpress;
using CDTDatabase;
using CDTLib;
using Plugins;
using System.Data;
using DevExpress.XtraEditors;

namespace TaoSoCT
{
    public class TaoSoCT:ICData
    {
        private InfoCustomData _info;
        private DataCustomData _data;
        Database db = Database.NewDataDatabase();
        Database dbCDT = Database.NewStructDatabase();
        DataRow drMaster;

        #region ICData Members
 
        public TaoSoCT()
        {
            _info = new InfoCustomData(IDataType.MasterDetailDt);
        }

        public DataCustomData Data
        {
            set { _data = value; }
        }

        public void ExecuteAfter()
        {
            
        }

        void CreateCT()
        {            
            if (drMaster.RowState == DataRowState.Modified || drMaster.RowState == DataRowState.Deleted)
                return;
            if (!drMaster.Table.Columns.Contains("SoCT") || !drMaster.Table.Columns.Contains("NgayCT"))
                return;
            if (_data.DrTable["MaCT"].ToString() == "")
                return;
            string sql = "", soctNew = "", MaCT = "", MaCN = "", prefix = "";
            MaCT = _data.DrTable["MaCT"].ToString();
            if (Config.GetValue("MaCN") != null)
                MaCN = Config.GetValue("MaCN").ToString();
            if (MaCN == "")
                return;
            DateTime NgayCT = (DateTime)drMaster["NgayCT"];
            string Nam = NgayCT.Year.ToString();
            string Thang = NgayCT.Month.ToString();
            Nam = Nam.Substring(2, 2);
            prefix = MaCT + "/" + MaCN + "/" + Nam + "/" + Thang + "/";
            sql = string.Format(@"SELECT TOP 1 CAST(REPLACE(SoCT,'{0}','') AS INT) [SoCT]
                                        FROM	{1}
                                        WHERE	SoCT LIKE '{0}%' 
                                                AND ISNUMERIC(REPLACE(SoCT,'{0}','')) = 1
                                        ORDER BY CAST(REPLACE(SoCT,'{0}','') AS INT) DESC"
                                        , prefix, _data.DrTableMaster["TableName"].ToString());

            using (DataTable dt = _data.DbData.GetDataTable(sql))
            {
                if (dt.Rows.Count > 0)
                {
                    int i = (int)dt.Rows[0]["SoCT"] + 1;
                    soctNew = prefix + i.ToString();
                    //soctNew = prefix + i.ToString("D3");
                }
                else
                {
                    //soctNew = prefix + "001";
                    soctNew = prefix + "1";
                }
            }
            drMaster["SoCT"] = soctNew;            
        }
        
        public void ExecuteBefore()
        {
            if (_data.CurMasterIndex < 0)
                return;            
            drMaster = _data.DsData.Tables[0].Rows[_data.CurMasterIndex];

            // kiểm tra những phiếu nào được tạo ra từ màn hình Quản lý học viên thuộc tab Thu Phí
            // thì không cho phép sửa/xóa. Chỉ cho thay đổi duyệt.
            string _tableName = _data.DrTableMaster["TableName"].ToString();
            if (_tableName == "MT31" || _tableName == "MT11" || _tableName == "MT15" || _tableName == "MT43")
            {
                // nếu không phải thay đổi duyệt mà thay đổi dữ liệu khác.
                if (drMaster.RowState == DataRowState.Unchanged
                    || drMaster.RowState == DataRowState.Deleted
                    || (drMaster.RowState == DataRowState.Modified
                        && ((bool)drMaster["Duyet", DataRowVersion.Original]) == (bool)drMaster["Duyet"]))
                {
                    // kiểm tra phiếu này có phải được tạo ra từ menu Quản lý học viên thuộc tab Thu Phí không
                    // nếu có thì kiểm tra tiếp master or detail có bị thay đổi/xóa không
                    // nếu có thì thông báo không cho phép sửa/xóa.
                    string PrimaryKeyField = _tableName + "ID";
                    if (isCreatedFrom_ThuHocPhi(PrimaryKeyField))
                    {
                        if (isChangeMaster() || isChangesDetail(PrimaryKeyField))
                        {
                            _info.Result = false;
                            XtraMessageBox.Show("Không thể sửa/xóa khi phiếu được tạo ra từ tab [ThuPhí] thuộc màn hình [Quản lý học viên]!",
                                Config.GetValue("PackageName").ToString());
                            return;
                        }
                    }
                }
            }

            CreateCT();
        }
        private bool isChangeMaster()
        {
            if (drMaster.RowState == DataRowState.Modified || drMaster.RowState == DataRowState.Deleted)
                return true;
            return false;
        }
        private bool isCreatedFrom_ThuHocPhi(string PrimaryKeyField)
        {
            string MTID = drMaster[PrimaryKeyField, DataRowVersion.Original].ToString();
            string sql;
            if (PrimaryKeyField == "MT11ID" || PrimaryKeyField == "MT15ID") //trường hợp này cần kiểm tra thêm bảng CTThuTien (thu lần 2 trở đi)
                sql = string.Format(
                    @"If Exists (Select ID From DTDKLop Where {0} = '{1}' union all select DTDKLop from CTThuTien where {0} = '{1}') Select 1 Else Select 0"
                    , PrimaryKeyField, MTID);
            else
                sql = string.Format(
                    @"If Exists (Select ID From DTDKLop Where {0} = '{1}') Select 1 Else Select 0"
                    , PrimaryKeyField, MTID);
            if ((int)db.GetValue(sql) == 1)
                return true;
            return false;
        }
        private bool isChangesDetail(string PrimaryKeyField)
        {
            using (DataView dv = GetDetailView(PrimaryKeyField))
            {
                foreach (DataRowView drv in dv)
                {
                    if (drv.Row.RowState == DataRowState.Added
                        || drv.Row.RowState == DataRowState.Modified
                            || drv.Row.RowState == DataRowState.Deleted)
                        return true;
                }
            }
            return false;
            //return data.DsData.Tables[1].GetChanges(DataRowState.Deleted | DataRowState.Modified | DataRowState.Added);
        }
        private DataView GetDetailView(string PrimaryKeyField)
        {
            string MTID = drMaster.RowState != DataRowState.Deleted ? drMaster[PrimaryKeyField].ToString() : drMaster[PrimaryKeyField, DataRowVersion.Original].ToString();
            DataView dv = new DataView(_data.DsData.Tables[1]);
            dv.RowStateFilter = DataViewRowState.CurrentRows | DataViewRowState.Deleted;
            dv.RowFilter = PrimaryKeyField + " = '" + MTID + "'";
            return dv;
        }
        public InfoCustomData Info
        {
            get { return _info; }
        }

        #endregion
    }
}
