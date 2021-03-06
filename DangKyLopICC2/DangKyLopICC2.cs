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
using System.Drawing;

namespace DangKyLopICC2
{
    public class DangKyLopICC2 : ICControl
    {
        private InfoCustomControl info = new InfoCustomControl(IDataType.Detail);
        private DataCustomFormControl data;
        Database db = Database.NewDataDatabase();
        DataRow drMaster;
        GridView gvMain;
        GridControl gcMain;
        GridView gvDetail;
        GridControl gcDetail;
        BindingSource bsMain;
        DataTable dtCTGioHoc;

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

            gcDetail = data.FrmMain.Controls.Find("gcDetail", true)[0] as GridControl;
            gvDetail = gcDetail.MainView as GridView;

            gcDetail.Width = gcDetail.Width + 90;

            gvDetail.Columns["HVTVID"].VisibleIndex = 0;
            gvDetail.Columns["NgayDK"].VisibleIndex = 3;
            gvDetail.Columns["SBGT"].VisibleIndex = gvDetail.Columns["HocPhi"].VisibleIndex + 1;
            gvDetail.Columns["SoBuoi"].VisibleIndex = gvDetail.Columns["HocPhi"].VisibleIndex + 2;
            gvDetail.Columns["TienBL"].VisibleIndex = gvDetail.Columns["HPGT"].VisibleIndex + 1;
            gvDetail.Columns["NgayKT"].VisibleIndex = gvDetail.Columns["GhiChu"].VisibleIndex - 1;
            gvDetail.Columns["MaCD"].VisibleIndex = -1;
            gvDetail.Columns["TConLai"].Fixed = DevExpress.XtraGrid.Columns.FixedStyle.Right;
            gvDetail.Columns["TDaNop"].Fixed = DevExpress.XtraGrid.Columns.FixedStyle.Right;
            
            //lấy sẵn bảng giờ học để phục vụ nhảy ngày học
            dtCTGioHoc = db.GetDataTable(@"SELECT	MaGioHoc, Thu, [Value] FROM	CTGioHoc");
            //Chức năng thu học phí nhiều lần
            gvDetail.MouseUp += new MouseEventHandler(gvDetail_MouseUp);

            if (data.BsMain.DataSource != null)
            {
                bsMain = data.BsMain.DataSource as BindingSource;
                bsMain.DataSourceChanged += new EventHandler(BsMain_DataSourceChanged);
                BsMain_DataSourceChanged(bsMain, new EventArgs());
            }
        }

        void gvDetail_MouseUp(object sender, MouseEventArgs e)
        {
            if (gvDetail.DataRowCount == 0)
                return;
            if (gvDetail.Columns["TConLai"].OptionsColumn.AllowFocus == false)
                gvDetail.Columns["TConLai"].OptionsColumn.AllowFocus = true;
            if (gvDetail.Columns["TConLai"] != gvDetail.FocusedColumn)
                return;
            DataSet ds = bsMain.DataSource as DataSet;
            if (ds == null) return;
            DataTable dtChange = ds.Tables[0].GetChanges();
            if (dtChange != null && dtChange.Rows.Count > 0)
            {
                XtraMessageBox.Show("Vui lòng cập nhật dữ liệu",
                    Config.GetValue("PackageName").ToString());
                return;
            }
            ThuHP();
        }

        private void ThuHP()
        {
            DataRow dr = gvDetail.GetDataRow(gvDetail.FocusedRowHandle);
            //lấy thông tin học viên
            DataTable dtHV = db.GetDataTable(string.Format("select MaHV, TenHV from DMHVTV where HVTVID = '{0}'", dr["HVTVID"]));
            if (dtHV == null || dtHV.Rows.Count == 0)
                return;
            DataRow drHV = dtHV.Rows[0];
            //lấy dữ liệu thu HP nhiều lần
            string sql = @"Select NgayThu,SoTien,HTTT,NguoiThu,MT11ID,MT15ID,DTDKLop From CTThuTien where dtdklop = '" + dr["ID"] + "'";
            DataTable dtHPNL = db.GetDataTable(sql);

            if (Convert.ToDecimal(dr["TConLai"]) > 0
                || (Convert.ToDecimal(dr["TConLai"]) <= 0 && dtHPNL != null && dtHPNL.Rows.Count > 0))
            {
                frmThuHocPhi frmHP = new frmThuHocPhi(dtHPNL, dr, drHV);
                if (frmHP.ShowDialog() == DialogResult.OK)  //có sự thay đổi học phí thu -> cập nhật DTDKLop
                {
                    data.FrmMain.Activate();
                    SendKeys.SendWait("{F12}");
                }
            }
        }

        private void BsMain_DataSourceChanged(object sender, EventArgs e)
        {
            DataSet ds = bsMain.DataSource as DataSet;
            if (ds == null) return;
            if (bsMain.Current != null)
                drMaster = (bsMain.Current as DataRowView).Row;
            // Tab thu học phí (DTDKLop)             
            ds.Tables[0].ColumnChanged += new DataColumnChangeEventHandler(DangKyLopICC_ColumnChanged);
            ds.Tables[0].TableNewRow += new DataTableNewRowEventHandler(DangKyLopICC2_TableNewRow);
        }

        void DangKyLopICC2_TableNewRow(object sender, DataTableNewRowEventArgs e)
        {
            if (bsMain.Current == null)
                return;
            drMaster = (bsMain.Current as DataRowView).Row;
            e.Row["MaCD"] = drMaster["MaNLop"];
        }

        private DayOfWeek OfWeek(string Value)
        {
            DayOfWeek _DayOfWeek = DayOfWeek.Monday;
            switch (Value)
            {
                case "2":
                    _DayOfWeek = DayOfWeek.Monday;
                    break;
                case "3":
                    _DayOfWeek = DayOfWeek.Tuesday;
                    break;
                case "4":
                    _DayOfWeek = DayOfWeek.Wednesday;
                    break;
                case "5":
                    _DayOfWeek = DayOfWeek.Thursday;
                    break;
                case "6":
                    _DayOfWeek = DayOfWeek.Friday;
                    break;
                case "7":
                    _DayOfWeek = DayOfWeek.Saturday;
                    break;
                case "1":
                    _DayOfWeek = DayOfWeek.Sunday;
                    break;
            }
            return _DayOfWeek;
        }
        
        void DangKyLopICC_ColumnChanged(object sender, DataColumnChangeEventArgs e)
        {   //chỉ cần chạy sau khi đã gán mã cấp độ (ở sự kiện tablenewrow)
            if (e.Row.RowState == DataRowState.Deleted || e.Row["MaCD"] == DBNull.Value || e.Row["MaCD"].ToString() == "")
                return;
            drMaster = (bsMain.Current as DataRowView).Row;

            #region Nhảy ngày học (NgayDK) theo ngày đăng ký (NgayTN)
            if ((e.Column.ColumnName.ToUpper() == "NGAYTN" && e.Row["NgayTN"] != DBNull.Value)
                || (e.Column.ColumnName.ToUpper() == "MACD" && e.Row["NgayTN"] != DBNull.Value))
            {
                try
                {
                    DateTime ngayKG = Convert.ToDateTime(drMaster["NgayBDKhoa"]);
                    DateTime ngayDK = Convert.ToDateTime(e.Row["NgayTN"]);
                    if (ngayDK <= ngayKG)   //đăng ký trước ngày khai giảng của lớp
                        e.Row["NgayDK"] = ngayKG;
                    else
                    {
                        bool valid = false;
                        DataRow[] drCTGioHoc = dtCTGioHoc.Select(string.Format(" MaGioHoc = '{0}' ", drMaster["MaGioHoc"].ToString()));
                        DateTime ngayKT = Convert.ToDateTime(drMaster["NgayKTKhoa"]);
                        while (ngayDK < ngayKT)
                        {
                            foreach (DataRow drGH in drCTGioHoc)
                                if (ngayDK.DayOfWeek == OfWeek(drGH["Value"].ToString()))
                                {
                                    e.Row["NgayDK"] = ngayDK;
                                    valid = true;
                                    break;
                                }
                            if (valid)
                                break;
                            ngayDK = ngayDK.AddDays(1);
                        }
                        if (!valid)
                            XtraMessageBox.Show("Không thể đăng ký sau ngày kết thúc của lớp",
                                Config.GetValue("PackageName").ToString());
                    }
                    e.Row.EndEdit();
                }
                catch (Exception ex)
                {
                    XtraMessageBox.Show("Lỗi khi tính ngày học\n" + ex.Message,
                        Config.GetValue("PackageName").ToString());
                }
            }
            #endregion

            #region Tiền bảo lưu
            if (e.Column.ColumnName.ToUpper() == "HVTVID"
                || e.Column.ColumnName.ToUpper() == "SOBUOI"
                || e.Column.ColumnName.ToUpper() == "SBGT"
                || e.Column.ColumnName.ToUpper() == "TIENCK"                
                || e.Column.ColumnName.ToUpper() == "HPGT"
                || e.Column.ColumnName.ToUpper() == "HOCPHI"
                || e.Column.ColumnName.ToUpper() == "NGAYDK")
                
            {
                if (e.Row["HVTVID"] != DBNull.Value
                    && e.Row["SoBuoi"] != DBNull.Value
                    && e.Row["SBGT"] != DBNull.Value
                    && e.Row["TienCK"] != DBNull.Value
                    && e.Row["HPGT"] != DBNull.Value
                    && e.Row["HocPhi"] != DBNull.Value)
                {
                    int soBuoi = (int)e.Row["SoBuoi"];
                    int SBGT = (int)e.Row["SBGT"];
                    decimal tienCK = (decimal)e.Row["TienCK"];
                    decimal HPGT = (decimal)e.Row["HPGT"];
                    decimal hocPhi = (decimal)e.Row["HocPhi"];
                    if (soBuoi > 0)
                    {
                        decimal tienHP = Math.Round(hocPhi / (soBuoi + SBGT) * soBuoi - (tienCK + HPGT), 3);
                        string refMTDKBL = "";
                        e.Row["TienBL"] = TienBL(e.Row["HVTVID"] != DBNull.Value ? (int)e.Row["HVTVID"] : -1, tienHP, ref refMTDKBL, e.Row);
                        if (refMTDKBL != "")
                            e.Row["refMTDKBL"] = refMTDKBL;
                        else
                            e.Row["refMTDKBL"] = DBNull.Value;
                    }
                }
            }
            #endregion

            #region  Nhảy học phí + Nhảy số buổi và giảm trừ (số buổi đã khai giảng trước đó)
            if ((e.Column.ColumnName.ToUpper() == "NGAYDK" || e.Column.ColumnName.ToUpper() == "NGAYTN"
                                      || e.Column.ColumnName.ToUpper() == "MACD")
                            && e.Row["NgayTN"] != DBNull.Value
                                && e.Row["MaCD"] != DBNull.Value)
            {
                string sMaLop = drMaster["MaLop"].ToString();
                string macd = e.Row["MaCD"].ToString();
                DateTime ngayDK = Convert.ToDateTime(e.Row["NgayTN"]);

                e.Row["HocPhi"] = GetHocPhi(macd, ngayDK);

                if (e.Row["NgayDK"] != DBNull.Value)
                {
                    DateTime ngayHoc = Convert.ToDateTime(e.Row["NgayDK"]);
                    string sql = string.Format(@"   
                            select	count(id)
                            from	templichhoc t 
                            where	malop = '{0}' and ngay < '{1}'"
                                , sMaLop, ngayHoc);
                    object o = db.GetValue(sql);
                    if (o != null && o.ToString() != "")
                        e.Row["SBGT"] = (int)o;
                    else
                        e.Row["SBGT"] = 0;
                    if (drMaster["TSB"] != DBNull.Value && drMaster["TSB"].ToString() != "")
                        e.Row["SoBuoi"] = Convert.ToInt32(drMaster["TSB"]) - Convert.ToInt32(e.Row["SBGT"]);
                    else
                        XtraMessageBox.Show("Lớp chưa có số buổi học, không tính được học phí cho học viên!",
                            Config.GetValue("PackageName").ToString());
                }
                e.Row.EndEdit();
            }
            #endregion 

            #region Tính ngày hết học phí theo thực nộp
            if ((e.Column.ColumnName.ToUpper() == "NGAYDK"
                                      || e.Column.ColumnName.ToUpper() == "MACD" || e.Column.ColumnName.ToUpper() == "TDANOP")
                            && e.Row["NgayDK"] != DBNull.Value && e.Row["TienHP"] != DBNull.Value
                                && e.Row["MaCD"] != DBNull.Value && e.Row["TDaNop"] != DBNull.Value)
            {
                string sMaLop = drMaster["MaLop"].ToString();
                DateTime ngayDK = Convert.ToDateTime(e.Row["NgayDK"]);
                int sbcl = Convert.ToInt32(e.Row["SoBuoi"]);
                decimal tienHP = Convert.ToDecimal(e.Row["TienHP"]);
                decimal nopHP = Convert.ToDecimal(e.Row["TDaNop"]) - Convert.ToDecimal(e.Row["TienGT"]);
                if (sbcl > 0 && tienHP > 0 && nopHP > 0)
                {
                    decimal sbdh = Math.Round(nopHP / (tienHP / sbcl), 0);
                    e.Row["NgayKT"] = TinhNgayKT(sMaLop, ngayDK, (int)sbdh);
                    e.Row.EndEdit();
                }
            }
            #endregion

            #region Chọn mua giáo trình
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
        // ngày hết học phí
        private DateTime TinhNgayKT(string MaLop, DateTime NgayBD, int SoBuoic)
        {
            DataTable dt = db.GetDataTable(string.Format("exec TinhNgayKT '{0}','{1}', '{2}'", SoBuoic, NgayBD, MaLop));
            // tính theo số buổi được học của học viên khi đóng tiền
            if (dt.Rows.Count == 0 || dt.Rows[0]["NgayKT"] == DBNull.Value)
            {
                return DateTime.MinValue;
            }
            DateTime NgayKT = DateTime.Parse(dt.Rows[0]["NgayKT"].ToString());

            return NgayKT;
        }
        //Tiền bảo lưu
        private decimal TienBL(int hvtvid, decimal tienHP, ref string refMTDKBL, DataRow dr)
        {
            // lấy thông tin bảo lưu.
            string sql = "";
            bool isUsedBaoLuu = false;
            if (dr.RowState == DataRowState.Modified && dr["refMTDKBL", DataRowVersion.Original] != DBNull.Value)
            {
                sql = string.Format("Select BLSoTien, HVID From MTDK Where HVID = '{0}'", dr["refMTDKBL",DataRowVersion.Original]);
                isUsedBaoLuu = true;
            }
            else
                sql = string.Format("Select Top 1 BLSoTien, HVID From MTDK Where HVTVID = {0} and BLSoTien > 0 Order By NgayBL Desc", hvtvid);

            DataTable dtInfo = db.GetDataTable(sql);
            if (dtInfo.Rows.Count > 0)
            {
                decimal tienBL = isUsedBaoLuu ? (decimal)dtInfo.Rows[0]["BLSoTien"] + (decimal)dr["TienBL",DataRowVersion.Original] : (decimal)dtInfo.Rows[0]["BLSoTien"];
                refMTDKBL = dtInfo.Rows[0]["HVID"].ToString();
                return tienBL <= tienHP ? tienBL : tienHP;
            }
            else
                return 0;
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
