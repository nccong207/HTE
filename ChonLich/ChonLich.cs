using System;
using System.Collections.Generic;
using System.Text;
using Plugins;
using System.Windows.Forms;
using CDTDatabase;
using CDTLib;
using DevExpress;
using DevExpress.XtraEditors;
using System.Data;
using DevExpress.XtraLayout;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid;
using DevExpress.XtraTab;

namespace ChonLich
{
    class ChonLich:ICControl
    {
        private DataCustomFormControl data;
        private InfoCustomControl info = new InfoCustomControl(IDataType.MasterDetailDt);
        private Database db = Database.NewDataDatabase();


        MemoEdit teGioHoc;
        GridView gvChonLich;
        XtraTabControl tcMain;
        TextEdit teTenHV;
        public void AddEvent()
        {
           
            teGioHoc = data.FrmMain.Controls.Find("GioHoc", true)[0] as MemoEdit;
            gvChonLich = (data.FrmMain.Controls.Find("DTChonLich",true)[0] as GridControl).MainView as GridView;
            tcMain = data.FrmMain.Controls.Find("tcMain", true)[0] as XtraTabControl;
            teTenHV = data.FrmMain.Controls.Find("TenHV", true)[0] as TextEdit;
            foreach (XtraTabPage i in tcMain.TabPages)
            {
                if (i.Text.Contains("Chọn lịch có thể học"))
                {
                    i.PageVisible = false;
                }
            }
            LayoutControl lc = data.FrmMain.Controls.Find("lcMain",true)[0] as LayoutControl;
            SimpleButton btChonLich = new SimpleButton();
            btChonLich.Name = "btChonLich";
            btChonLich.Text = "Chọn lịch chờ";
            LayoutControlItem lci1 = lc.AddItem("", btChonLich);
            lci1.Name = "cusChonLich"; //phai co name cua item, bat buoc phai co "cus" phai truoc
            btChonLich.Click += new EventHandler(btChonLich_Click);
        }

        void btChonLich_Click(object sender, EventArgs e)
        {
           if (teTenHV.Properties.ReadOnly == true)
           {
               return;
           }
           DataRowView drMaster = data.BsMain.Current as DataRowView;
           string hvid = drMaster["HVTVID"].ToString();
           frmChonLich frm = new frmChonLich(hvid);
           frm.Text = "Chọn lịch học";
           frm.ShowDialog();
            
           //set lại giờ học
           if (frm.DialogResult == DialogResult.OK)
           {
               teGioHoc.Text = "";
               teGioHoc.EditValue = frm.giohoc;
               
               DataSet dset = data.BsMain.DataSource as DataSet;
               DataTable dtLH = dset.Tables[5]; //table chọn lịch
               //DataTable dtLH = dset.Tables["DTChonLich"];
               //Xóa lịch cũ
               DataRow[] drCLich = dtLH.Select("HVTVID  " + (hvid == "" ? "is null" : ("=" + hvid)));
               foreach (DataRow dr in drCLich)
               {
                   dr.Delete();
               }
               //Thêm lịch mới vào dtlichhoc
               foreach (DataRow dr in frm.dtLuu.Rows)
               {
                   DataRow drnew = dtLH.NewRow();
                   dtLH.Rows.Add(drnew);
                   if (hvid != "")
                       drnew["HVTVID"] = hvid;
                   drnew["LichHocID"] = dr["id"];
               }
               data.BsMain.EndEdit();
           }
        }

        public DataCustomFormControl Data
        {
            set { data = value; }
        }

        public InfoCustomControl Info
        {
            get { return info; }
        }
    }
}
