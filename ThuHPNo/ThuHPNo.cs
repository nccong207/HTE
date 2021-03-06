using System;
using System.Collections.Generic;
using System.Text;
using CDTDatabase;
using CDTLib;
using Plugins;
using System.Data;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid;
using System.Windows.Forms;

namespace ThuHPNo
{
    public class ThuHPNo : ICControl
    {
        private InfoCustomControl info = new InfoCustomControl(IDataType.MasterDetailDt);
        private DataCustomFormControl data;
        GridView gvMain;
        GridControl gcMain;
        Database db = Database.NewDataDatabase();
            
        public DataCustomFormControl Data
        {
            set { data = value; }
        }

        public InfoCustomControl Info
        {
            get { return info; }
        }

        public void AddEvent()
        {
            gcMain = data.FrmMain.Controls.Find("gcMain", true)[0] as GridControl;
            gvMain = gcMain.MainView as GridView;
            //Chức năng thu học phí nhiều lần
            gvMain.MouseUp += new MouseEventHandler(gvMain_MouseUp);
        }

        void gvMain_MouseUp(object sender, MouseEventArgs e)
        {
            if (gvMain.Editable || gvMain.DataRowCount == 0)
                return;
            if (gvMain.Columns["TConLai"] != gvMain.FocusedColumn)
                return;
            ThuHP();
        }

        private void ThuHP()
        {
            DataRow dr = gvMain.GetDataRow(gvMain.FocusedRowHandle);
            DataRow drMaster = (data.BsMain.Current as DataRowView).Row;
            string sql = @"Select NgayThu,SoTien,HTTT,NguoiThu,MT11ID,MT15ID,DTDKLop From CTThuTien where dtdklop = '" + dr["ID"] + "'";
            DataTable dtHPNL = db.GetDataTable(sql);

            if (Convert.ToDecimal(dr["TConLai"]) > 0
                || (Convert.ToDecimal(dr["TConLai"]) <= 0 && dtHPNL != null && dtHPNL.Rows.Count > 0))
            {
                frmThuHocPhi frmHP = new frmThuHocPhi(dtHPNL, dr, drMaster);
                frmHP.ShowDialog();
                DataRow drCur = frmHP._drvDKLop;
                if (Convert.ToDecimal(dr["TDaNop"]) != Convert.ToDecimal(drCur["TDaNop"])
                        && Convert.ToDecimal(dr["TConLai"]) != Convert.ToDecimal(drCur["TConLai"]))
                {
                    gvMain.SetFocusedRowCellValue(gvMain.Columns["TDaNop"], drCur["TDaNop"]);
                    gvMain.SetFocusedRowCellValue(gvMain.Columns["TConLai"], drCur["TConLai"]);
                }
            }
        }
    }
}
