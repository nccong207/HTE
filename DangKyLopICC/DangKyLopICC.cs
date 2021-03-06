using System;
using System.Collections.Generic;
using System.Text;
using CDTControl;
using CDTDatabase;
using CDTLib;
using Plugins;
using System.Data;
using System.Windows.Forms;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid;
using DevExpress.XtraEditors;
using DevExpress.XtraLayout;
using FormFactory;
using DevExpress.XtraEditors.Repository;

namespace DangKyLopICC
{ 
    public class DangKyLopICC : ICControl
    {
        private InfoCustomControl info = new InfoCustomControl(IDataType.MasterDetailDt);
        private DataCustomFormControl data;
        Database db = Database.NewDataDatabase();
        DataRow drMaster;
        GridView gvMain;
        GridControl gcMain;

        // 2 biến này dùng cho xử lý [CẬP NHẬT TIỀN BẢO LƯU CHO MTDK] bên ICD [SaveDangKyLop] 
        // trong hàm [UpdateAllDatas()].
        public static decimal m_TongTienBL = 0;
        public static string s_MTDKID = "";
            
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
            gvMain.Columns["MaCD"].VisibleIndex = 0;
            gvMain.Columns["NgayTN"].VisibleIndex = 1;
            gvMain.Columns["HVTVID"].VisibleIndex = -1;
            gvMain.Columns["SBGT"].VisibleIndex = -1;
            gvMain.Columns["SoBuoi"].VisibleIndex = -1;
            gvMain.Columns["NgayDK"].VisibleIndex = -1;
            gvMain.Columns["NgayKT"].VisibleIndex = -1;
            gvMain.Columns["TienBL"].VisibleIndex = -1;
            gvMain.ActiveFilterString = "[MaLop] Is Null";  //ẩn DTDKLop có mã lớp
            LayoutControl lcMain = data.FrmMain.Controls.Find("lcMain", true)[0] as LayoutControl;
            data.BsMain.DataSourceChanged += new EventHandler(BsMain_DataSourceChanged);
            BsMain_DataSourceChanged(data.BsMain, new EventArgs());
        }

        private void BsMain_DataSourceChanged(object sender, EventArgs e)
        {
            DataSet ds = data.BsMain.DataSource as DataSet;
            if (ds == null) return;
            if (data.BsMain.Current != null)
                drMaster = (data.BsMain.Current as DataRowView).Row;
            // Tab thu học phí (DTDKLop)             
            ds.Tables[1].ColumnChanged += new DataColumnChangeEventHandler(DangKyLopICC_ColumnChanged);
        } 
        
        void DangKyLopICC_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {
            if (e.Row.RowState == DataRowState.Deleted)
                return;
            drMaster = (data.BsMain.Current as DataRowView).Row;
            if (drMaster.RowState == DataRowState.Deleted)
                return; 
            //--Nhảy tiền bảo lưu --//
            #region Tiền bảo lưu
            //if (e.Column.ColumnName.ToUpper() == "HOCPHI")
            //{
            //    e.Row["TienBL"] = TienBL(drMaster["HVTVID"] != DBNull.Value ? (int)drMaster["HVTVID"] : -1);
            //}
            #endregion

            #region Nhảy số buổi
            //---- Nhảy số buổi ----//
//            if (e.Column.ColumnName.ToUpper() == "NGAYDK"
//                 || e.Column.ColumnName.ToUpper() == "SOTHANG"
//                    || e.Column.ColumnName.ToUpper() == "MALOP"
//                       || e.Column.ColumnName.ToUpper() == "CACHTINHHP")
//            {                

//                if (e.Row["CachTinhHP"] == DBNull.Value || e.Row["CachTinhHP"].ToString().ToUpper() != "THEO BUỔI")
//                    e.Row["SoBuoi"] = 0;              
//                else
//                {
//                    if (e.Row["NgayDK"] == DBNull.Value
//                        || e.Row["SoThang"] == DBNull.Value
//                        || e.Row["MaLop"] == DBNull.Value)
//                        return;
//                    DateTime dtNgayDK = (DateTime)e.Row["NgayDK"];
//                    int iSothang = (int)e.Row["SoThang"];
//                    string sMaLop = e.Row["MaLop"].ToString();

//                    string sql = string.Format(@"   
//                            select	count(id)
//                            from	templichhoc t 
//                            where	malop = '{0}' and magio = (select magiohoc from dmlophoc where malop = '{0}')
//                            and ngay between '{1}' and dateadd(m,{2},'{1}')"
//                                , sMaLop, dtNgayDK, iSothang);
//                    e.Row["SoBuoi"] = (int)db.GetValue(sql);
//                }
//            }
            #endregion 
          
            #region Lấy [CachTinhHP + DonGia] + Nhảy học phí + Chọn học thử
            //---- Nhảy học phí ----//
            if ((e.Column.ColumnName.ToUpper() == "NGAYTN"
                                      || e.Column.ColumnName.ToUpper() == "MACD")
                            && e.Row["NgayTN"] != DBNull.Value
                                && e.Row["MaCD"] != DBNull.Value)
            {
                if (e.Row["SoBuoi"] == DBNull.Value || Convert.ToInt32(e.Row["SoBuoi"]) == 0)    //dang ky cho lop set mac dinh so buoi = 1 de nhay cthuc TienHP trong CDT
                    e.Row["SoBuoi"] = 1;
                string macd = e.Row["MaCD"].ToString();
                DateTime ngayDK = Convert.ToDateTime(e.Row["NgayTN"]);
                e.Row["HocPhi"] = GetHocPhi(macd, ngayDK);
                e.Row.EndEdit();
            }
            #endregion

            #region Tính tiền HP giảm trừ khi đăng ký sau khi khai giảng
            //if (e.Column.ColumnName.ToUpper() == "SBGT"
            //    || e.Column.ColumnName.ToUpper() == "DONGIA")
            //{
            //    if (e.Row["CachTinhHP"].ToString().ToUpper() == "THEO BUỔI")
            //    {
            //        int SBGT = e.Row["SBGT"] != DBNull.Value ? (int)e.Row["SBGT"] : 0;
            //        decimal DonGia = (decimal)e.Row["DonGia"];
            //        e.Row["HPGT"] = SBGT * DonGia;
            //    }
            //}
            #endregion

            #region Chọn mua bàn tính
            //---- Nhảy TienGT ----//
            //Khi check vào IsMuaBT, 
            //dựa vào sum dữ liệu từ bảng vật tư nhóm lớp để lấy tổng đơn giá bán của vật tư và điền vào cột TienGT.
            if (e.Column.ColumnName.ToUpper() == "ISMUABT" || e.Column.ColumnName.ToUpper() == "MACD")
            {
                if (e.Row["ISMUABT"] == DBNull.Value)
                    return;
                if (!(bool)e.Row["ISMUABT"])
                {
                    e.Row["TienGT"] = 0;
                    e.Row.EndEdit();
                    return;
                }
                // Có chọn mua bàn tính
                if (e.Row["MaCD"] == DBNull.Value)
                {
                    e.Row["TienGT"] = 0;
                    e.Row.EndEdit();
                    return;
                }
                string sMaNhomLop = e.Row["MaCD"].ToString();
                string sql = string.Format(@"
                                Select ISNULL(Sum(vt.GiaBan),0) 
                                From VTNL vl inner join DMVT vt on vt.MaVT = vl.MaVT 
                                Where vl.MaNLop = '{0}'", sMaNhomLop);
                e.Row["TienGT"] = (decimal)db.GetValue(sql);
                e.Row.EndEdit();
            }
            
            #endregion

            if ((e.Column.ColumnName.ToUpper() == "TIENHP")   //cập nhật thực nộp = tổng tiền (trừ trường hợp modified và check giáo trình)
                || (e.Column.ColumnName.ToUpper() == "TIENGT" &&
                    ((e.Row.RowState == DataRowState.Modified && !Convert.ToBoolean(e.Row["isMuaBT"]))
                    || (e.Row.RowState != DataRowState.Modified))))
            {
                e.Row["TDaNop"] = e.Row["TTien"];
                e.Row.EndEdit();
            }

            if (e.Column.ColumnName.ToUpper() == "CTCKID")
                if (e.Row["CTCKID"] == DBNull.Value)
                    e.Row["TyLe"] = 0;
            
        }
        //Tiền bảo lưu
        private decimal TienBL(int hvtvid)
        { // lấy thông tin bảo lưu.
            string sql = string.Format("Select Top 1 BLSoTien, HVID From MTDK Where HVTVID = {0} Order By NgayBL Desc", hvtvid);
            DataTable dtInfo = db.GetDataTable(sql);
            if (dtInfo.Rows.Count > 0)
            {
                m_TongTienBL = (decimal)dtInfo.Rows[0]["BLSoTien"];
                s_MTDKID = dtInfo.Rows[0]["HVID"].ToString();
            }
            return m_TongTienBL;
        }
        //Get học phí theo DMHocPhi
        decimal GetHocPhi(string macd, DateTime ngayDK)
        {
            string sql = string.Format(@" 
                                            select top 1 nl.hocphi 
                                            from    dmhocphi hp inner join hpnl nl on hp.hpid = nl.hpid 
                                            where   nl.ngaybd <= '{0}' and hp.manl = '{1}' order by nl.ngaybd desc
                                        ",ngayDK,macd);
            object obj = db.GetValue(sql);
            return (obj == DBNull.Value ? 0 : Convert.ToDecimal(obj));
        }
    }
}
