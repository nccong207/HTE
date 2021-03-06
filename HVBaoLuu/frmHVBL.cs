using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using CDTDatabase;
using CDTLib;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Grid.ViewInfo;
using DevExpress.XtraEditors.Repository;
using FormFactory;
namespace HVBaoLuu
{
    public partial class frmHVBL : DevExpress.XtraEditors.XtraForm
    {
        public frmHVBL()
        {
            InitializeComponent();
        }
        bool flag = false;
        Database db = Database.NewDataDatabase();
        decimal sobuoiCL;
        DateTime ngaybl;
        decimal sbduochoc;
        decimal thucthu;
        int tuangiahan;
        private void frmHVBL_Load(object sender, EventArgs e)
        {            
            repositoryItemDateEdit1.EditValueChanged += new EventHandler(repositoryItemDateEdit1_EditValueChanged);
            string sql = "SELECT malop ,tenlop, SiSo FROM dmlophoc WHERE iskt = 0";
            DataTable dtlop = new DataTable();
            dtlop = db.GetDataTable(sql);
            grdmalop.Properties.DataSource = dtlop;
            grdmalop.Properties.DisplayMember = "malop";
            grdmalop.Properties.ValueMember = "malop";
            grdmalop.Properties.View.PopulateColumns();
            grdmalop.Properties.View.BestFitColumns();
           
        }
        
        
        void repositoryItemDateEdit1_EditValueChanged(object sender, EventArgs e)
        {                        
            
           
        }
                       
        void SetValue(DateEdit dateEdit)
        {
            string isBL = gvHocVien.GetFocusedRowCellValue("IsBL").ToString();
            if (isBL.ToUpper() == "FALSE")
                gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayHH"], null);
            else
            {
                DateTime dtp;
                int songay = 0;
                if (Config.GetValue("HanBaoLuu") != null)
                {
                    songay = int.Parse(Config.GetValue("HanBaoLuu").ToString());
                    dtp = DateTime.Parse(dateEdit.EditValue.ToString());
                    dtp = dtp.AddDays(songay);
                    gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayHH"], dtp);
                }
                //tiền bảo lưu
                if (gvHocVien.GetFocusedRowCellValue("NgayDK").ToString() != "" && gvHocVien.GetFocusedRowCellValue("HVID").ToString() != "")
                {
                    decimal sotien = TienBL(DateTime.Parse(gvHocVien.GetFocusedRowCellValue("NgayDK").ToString()), DateTime.Parse(dateEdit.EditValue.ToString()), gvHocVien.GetFocusedRowCellValue("HVID").ToString(), gvHocVien.GetFocusedRowCellValue("MaLop").ToString());
                    gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["BLSoTien"], sotien);
                    //gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["SoBuoiCL"], sobuocl);
                }
            }                 
        }

        void getDSLop()
        {
            string sql = "select HVID, TenHV, DK.NgayDK, DK.MaLop, LH.TenLop from DMLopHoc LH inner join MTDK DK on LH.MaLop = DK.MaLop " +
                "where isKT = '0' and dk.Duyet = 1 and  MaCN = '" + Config.GetValue("MaCN").ToString() + "'";
            if (txtTenHV.EditValue != null)
                sql += " and DK.TenHV like N'%" + txtTenHV.EditValue.ToString() + "%'";
            sql += " and IsNghiHoc = '0' {0} ";
            sql = String.Format(sql, showAll.Checked ? " " : " and isBL = '0' ");
            sql += " order by TenHV";
            DataTable dt = db.GetDataTable(sql);
            if (dt.Rows.Count == 0)
            {
                XtraMessageBox.Show("Không tìm thấy học viên hoặc lớp của học viên đã kết thúc!", Config.GetValue("PackageName").ToString());
                return;
            }
            lookupLop.Properties.DataSource = dt;
            lookupLop.Properties.DisplayMember = "MaLop";
            lookupLop.Properties.ValueMember = "MaLop";
            lookupLop.EditValue = dt.Rows[0]["MaLop"].ToString();
            
            lookupLop.Properties.BestFit();

            gridLookUpEditHV.Properties.View.Columns.Clear();
            gridLookUpEditHV.Properties.View.OptionsBehavior.AutoPopulateColumns = false;
            gridLookUpEditHV.Properties.View.OptionsView.ShowFilterPanelMode = DevExpress.XtraGrid.Views.Base.ShowFilterPanelMode.Never;
            gridLookUpEditHV.Properties.PopupBorderStyle = DevExpress.XtraEditors.Controls.PopupBorderStyles.NoBorder;

            gridLookUpEditHV.Properties.View.OptionsBehavior.AutoPopulateColumns = true;
            gridLookUpEditHV.Properties.DataSource = dt;
            gridLookUpEditHV.Properties.DisplayMember = "TenHV";
            gridLookUpEditHV.Properties.ValueMember = "HVID";

            gridLookUpEditHV.Properties.View.Columns["HVID"].Visible = false;
            gridLookUpEditHV.Properties.View.Columns["NgayDK"].Caption = "Ngày đăng ký";
            gridLookUpEditHV.Properties.View.Columns["NgayDK"].DisplayFormat.FormatType = DevExpress.Utils.FormatType.DateTime;
            gridLookUpEditHV.Properties.View.Columns["NgayDK"].DisplayFormat.FormatString = "dd/MM/yyyy";
            gridLookUpEditHV.Properties.View.Columns["TenHV"].Caption = "Tên học viên";
            gridLookUpEditHV.Properties.View.Columns["MaLop"].Caption = "Mã lớp"; 
            gridLookUpEditHV.Properties.View.Columns["TenLop"].Caption = "Tên lớp";

            gridLookUpEditHV.Properties.View.BestFitColumns();
            gridLookUpEditHV.Properties.PopupFormWidth = 500;
        }

        decimal TienBL(DateTime ngayDK, DateTime ngayBL, string HVID, string malop)
        {
            string sql = "select NgayBDKhoa, NgayKTKhoa, BDNghi, KTNghi, MaGioHoc from DMLopHoc where MaLop='" + malop + "'";
            DataTable dtLop = db.GetDataTable(sql);                        
            if (dtLop.Rows.Count == 0)
                return 0;
            decimal tienBL = 0;
            if (dtLop.Rows[0]["NgayBDKhoa"].ToString() != "" && dtLop.Rows[0]["NgayKTKhoa"].ToString() != "")
            {
                // % giảm HP + tiền nợ + tiền bảo lưu
                sql = "select * from mtdk where HVID ='" + HVID + "'";
                DataTable dt = db.GetDataTable(sql);
                if (dt.Rows.Count == 0)
                    return 0;
                sbduochoc = decimal.Parse(dt.Rows[0]["SoBuoiDH"].ToString());
                thucthu = decimal.Parse(dt.Rows[0]["ThucThu"].ToString());
                decimal phainop = decimal.Parse(dt.Rows[0]["TienHP"].ToString());
                decimal notruoc = decimal.Parse(dt.Rows[0]["HPNoTruoc"].ToString());
                // tính theo số buổi được học của học viên khi đóng tiền
                    DataTable dtbh = db.GetDataTable(string.Format("exec sp_SobuoiDHCL '{0}','{1}'", ngaybl, HVID));
                 sobuoiCL = decimal.Parse(dtbh.Rows[0]["sobuoidhcl"].ToString());

                 decimal giamHP = 0, tienNo = 0, tienConDu = 0;
                giamHP = decimal.Parse(dt.Rows[0]["GiamHP"].ToString());
                tienNo = decimal.Parse(dt.Rows[0]["ConLai"].ToString());
                tienConDu = (bool)dt.Rows[0]["IsBL"] ? 0 : decimal.Parse(dt.Rows[0]["BLSoTien"].ToString());
                // tổng tiền tất cả các lần thu
                thucthu = phainop - tienNo - tienConDu + notruoc; 
                //học phí chuẩn
                sql = " select HPNL.HocPhi, l.sobuoi  " +
                      " from dmlophoc l inner join dmhocphi hp on l.MaNLop=hp.MaNL  " +
                      " inner join HPNL on HPNL.HPID=hp.HPID " +
                      " inner join DMNhomLop NL on NL.MaNLop=hp.MaNL " +
                      " where l.MaLop='" + malop + "' " +
                      " and HPNL.NgayBD <='" + ngayDK.ToString() + "' order by HPNL.NgayBD DESC ";
                dt = db.GetDataTable(sql);
                decimal HPChuan = 0, sobuoiQD = 0; // so buoi quy dinh
                if (dt.Rows.Count > 0)
                {
                    HPChuan = decimal.Parse(dt.Rows[0]["HocPhi"].ToString());
                    if (giamHP != 0)
                        HPChuan = HPChuan - (HPChuan * giamHP) / 100;
                    sobuoiQD = decimal.Parse(dt.Rows[0]["SoBuoi"].ToString());
                    HPChuan = HPChuan / sobuoiQD;                    
                }
                string magio = dtLop.Rows[0]["MaGioHoc"].ToString();
                if (magio != "" && magio.Length > 1)
                    magio = magio.Substring(0,1);
                //sobuoiCL = SoNgayDu(DateTime.Parse(dtLop.Rows[0]["NgayBDKhoa"].ToString()), DateTime.Parse(dtLop.Rows[0]["NgayKTKhoa"].ToString()), ngayBL, magio, dtLop.Rows[0]["BDNghi"].ToString(), dtLop.Rows[0]["KTNghi"].ToString());
                //if (sobuoiCL > sobuoiQD) // nếu cho bảo lưu trước ngày học thì số buổi còn lại là số buổi quy định
                //    sobuoiCL = (int)sobuoiQD;
                if (sbduochoc > 0)
                    tienBL = sobuoiCL * (thucthu/sbduochoc);
                //Trừ đi số tiền nợ                                
                //tienBL -= tienNo;
                //Cộng tiền bảo lưu
                //tienBL += tienConDu;
            }
            tienBL = RoundNumber(tienBL);
            return tienBL;
        }

        decimal RoundNumber(decimal num)
        {
            num = num / 1000;
            num = Math.Round(num, 0);
            num *= 1000;
            return num;
        }

        private void btnHienThi_Click(object sender, EventArgs e)
        {
            if (flag)
            {
                DialogResult result = XtraMessageBox.Show("Dữ liệu thay đổi chưa được lưu.\n Bạn có muốn lưu không?",Config.GetValue("PackageName").ToString(),MessageBoxButtons.YesNo,MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    btnOK_Click(sender,e);
                }
            }

            if (lookupLop.EditValue == null)
                return;

            if (lookupLop.EditValue.ToString() != "")
            {
                showAll.Enabled = false;

                string sql = "select * from MTDK DK inner join DMHVTV TV on DK.HVTVID=TV.HVTVID "+
                    " where DK.MaLop = '" + lookupLop.EditValue.ToString() + "' and Duyet = 1 and IsNghiHoc = '0' {0}";
                sql = String.Format(sql, showAll.Checked ? " " : " and isBL = '0' ");
                
                DataTable dt = db.GetDataTable(sql);
                gcHocVien.DataSource = dt;
                //gvHocVien.BestFitColumns();
                dt.RowChanged += new DataRowChangeEventHandler(dt_RowChanged);                
                flag = false;
            }
        }        

        void dt_RowChanged(object sender, DataRowChangeEventArgs e)
        {
            flag = true;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            
            DataTable dt = gcHocVien.DataSource as DataTable;

            DataView dv = new DataView(dt);
            dv.RowStateFilter = DataViewRowState.ModifiedCurrent;
            string sql = "";
            foreach (DataRowView drv in dv)
            {
                if (drv["IsBL"].ToString().ToUpper() == "TRUE" && drv["NgayBL"].ToString() != "" && drv["NgayHH"].ToString() != "")
                {                    
                    //Cập nhật sỉ số đăng ký + tiền bảo lưu
                    if (drv.Row["IsBL", DataRowVersion.Original].ToString().ToUpper() != drv.Row["IsBL", DataRowVersion.Current].ToString().ToUpper())
                    {
                        //Trường hợp đăng ký vẫn còn dư tiền, giờ bảo lưu lớp đăng ký thì cộng dồn lại
                        //Tiền nợ thì cho về 0 vì đã trừ vào tiền bảo lưu.
                        sql = "Update MTDK set IsBL = '" + drv["IsBL"].ToString() + "', NgayBL = '" + drv["NgayBL"].ToString() + 
                            "', NgayHH = '" + drv["NgayHH"].ToString() + "', ConLai = '0', BLSoTien = BLSoTien +'" + drv["BLSoTien"].ToString() + 
                            "', NgayDKBL = '" + drv["NgayDKBL"].ToString() + "', SoBuoiBL = '" + drv["SoBuoiBL"].ToString() + "', SoTuanBL = '" + 
                            drv["SoTuanBL"].ToString() + "', LyDo = N'" + drv["LyDo"].ToString() + "' where HVID = '" + drv["HVID"].ToString() + "'";
                         db.UpdateByNonQuery(sql);
                         sql = "Update DMLophoc set SiSo = case when SiSo <= 0 then 0 else (SiSo - 1) end where MaLop = '" + drv["MaLop"].ToString() + "'";
                        db.UpdateByNonQuery(sql);
                    }
                    else
                    { //Trường hợp bảo lưu rồi giờ sửa lại thì gán giá trị mới
                        sql = "Update MTDK set IsBL = '" + drv["IsBL"].ToString() + "', NgayBL = '" + drv["NgayBL"].ToString() + "', NgayHH = '" + 
                            drv["NgayHH"].ToString() + "', BLSoTien =  '" + drv["BLSoTien"].ToString() + "', NgayDKBL = '" + drv["NgayDKBL"].ToString() +
                            "', SoBuoiBL = '" + drv["SoBuoiBL"].ToString() + "', SoTuanBL = '" + drv["SoTuanBL"].ToString() + "', LyDo = N'" + drv["LyDo"].ToString() + "' where HVID = '" + drv["HVID"].ToString() + "'";
                        db.UpdateByNonQuery(sql);
                    }
                }
                else if (drv["IsBL"].ToString().ToUpper() == "FALSE" && drv["NgayBL"].ToString() == "" && drv["NgayHH"].ToString() == "")
                {                    
                    //Cập nhật sỉ số đăng ký
                    if (drv.Row["IsBL", DataRowVersion.Original].ToString().ToUpper() != drv.Row["IsBL", DataRowVersion.Current].ToString().ToUpper())
                    {
                        sql = "Update MTDK set IsBL = '" + drv["IsBL"].ToString() + "', NgayBL = null, NgayHH = null,NgayDKBL=null,SoBuoiBL=null,SoTuanBL=null, BLSoTien = '0', LyDo = null where HVID = '" + drv["HVID"].ToString() + "'";
                        db.UpdateByNonQuery(sql);
                        sql = "Update DMLophoc set SiSo = (SiSo + 1) where MaLop = '" + drv["MaLop"].ToString() + "'";
                        db.UpdateByNonQuery(sql);
                    }
                }
            }
            if (dv.Count > 0)
            {
                XtraMessageBox.Show("Cập nhật thành công!", Config.GetValue("PackageName").ToString());
                dv.Table.AcceptChanges();
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
      

        private void lookupLop_EditValueChanged(object sender, EventArgs e)
        {
            showAll.Enabled = true;
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            getDSLop();
        }

        private void gridLookUpEditHV_EditValueChanged(object sender, EventArgs e)
        {                        
            if (gridLookUpEditHV.Properties.View.IsDataRow(gridLookUpEditHV.Properties.View.FocusedRowHandle))
            {                
                string sql = "select * from mtdk where HVID = '" + gridLookUpEditHV.EditValue.ToString() + "'";
                DataTable dt = db.GetDataTable(sql);
                gcHocVien.DataSource = dt;
                //gvHocVien.BestFitColumns();
                gvHocVien.Focus();               
            }
        }

        private void gvHocVien_CellValueChanged(object sender, DevExpress.XtraGrid.Views.Base.CellValueChangedEventArgs e)
        {
            if (e.Column.FieldName.ToUpper().Equals("ISBL"))
            {
                if (e.Value.ToString().ToUpper() == "TRUE")
                {
                    gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayDKBL"], DateTime.Today.ToString());
                    gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayBL"], DateTime.Today.ToString());
                    gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["SoBuoiBL"],0);
                    DateTime dtp;
                    int songay = 0;
                    //if (Config.GetValue("HanBaoLuu") != null)
                    //{
                    // Atlanta tính số ngày hết hạn bảo lưu  theo tuần : 1 tuần = 7 ngày
                    string s = gvHocVien.GetFocusedRowCellValue("SoTuanBL").ToString();
                   ngaybl=DateTime.Parse( gvHocVien.GetFocusedRowCellValue("NgayBL").ToString());
                    if (s == "")
                        return;
                    songay = int.Parse(s);
                        songay = int.Parse(Config.GetValue("HanBaoLuu").ToString());
                        dtp = DateTime.Today.AddDays(songay * 7);
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayHH"], dtp);
                    //}

                    //tiền bảo lưu
                    if (gvHocVien.GetFocusedRowCellValue("NgayDK").ToString() != "" && gvHocVien.GetFocusedRowCellValue("NgayBL").ToString() != "" && gvHocVien.GetFocusedRowCellValue("HVID").ToString() != "")
                    {
                        decimal sotien = TienBL(DateTime.Parse(gvHocVien.GetFocusedRowCellValue("NgayDK").ToString()), DateTime.Parse(gvHocVien.GetFocusedRowCellValue("NgayBL").ToString()), gvHocVien.GetFocusedRowCellValue("HVID").ToString(), gvHocVien.GetFocusedRowCellValue("MaLop").ToString());
                        //int SoBuoiBL=
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["SoBuoiBL"], sobuoiCL);
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["BLSoTien"], sotien);
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayDKBL"], DateTime.Today.ToString());
                    }
                }
                else
                {
                    gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["SoBuoiBL"],null);
                    gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayDKBL"], DateTime.Today);
                    gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayBL"], null);
                    gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayHH"], null);
                    gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["BLSoTien"], 0);
                }
            }
            if (e.Column.FieldName.ToUpper().Equals("SOTUANBL"))
            {
                string isBL = gvHocVien.GetFocusedRowCellValue("IsBL").ToString();
                if (isBL.ToUpper() == "FALSE")
                    gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayHH"], null);
                else
                {
                    gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["SoBuoiBL"], 0);
                    DateTime dtp;
                    int songay = 0;
                    ////if (Config.GetValue("HanBaoLuu") != null)
                    ////{
                    string s = gvHocVien.GetFocusedRowCellValue("SoTuanBL").ToString();
                    if (s == "")
                        return;
                    songay = int.Parse(s);
                        //songay = int.Parse(Config.GetValue("HanBaoLuu").ToString());
                    //dtp = DateTime.Parse(gvHocVien.GetFocusedRowCellValue("NgayBL").ToString());
                    ngaybl = DateTime.Parse(gvHocVien.GetFocusedRowCellValue("NgayBL").ToString());
                    dtp = DateTime.Parse(gvHocVien.GetFocusedRowCellValue("NgayBL").ToString());
                        dtp = ngaybl.AddDays(songay * 7);
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayHH"], dtp);
                    }
                    //tiền bảo lưu
                    if (gvHocVien.GetFocusedRowCellValue("NgayDK").ToString() != "" && e.Value.ToString() != "" && gvHocVien.GetFocusedRowCellValue("HVID").ToString() != "")
                    {
                        decimal sotien = TienBL(DateTime.Parse(gvHocVien.GetFocusedRowCellValue("NgayDK").ToString()), DateTime.Parse(gvHocVien.GetFocusedRowCellValue("NgayBL").ToString()), gvHocVien.GetFocusedRowCellValue("HVID").ToString(), gvHocVien.GetFocusedRowCellValue("MaLop").ToString());
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["BLSoTien"], sotien);
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["SoBuoiBL"], sobuoiCL);
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayDKBL"], DateTime.Today.ToString());
                    }
                }
                if (e.Column.FieldName.ToUpper().Equals("NGAYBL"))
                {
                    string isBL = gvHocVien.GetFocusedRowCellValue("IsBL").ToString();
                    if (isBL.ToUpper() == "FALSE")
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayHH"], null);
                    else
                    {
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["SoBuoiBL"], 0);
                        DateTime dtp;
                        int songay = 0;
                        ////if (Config.GetValue("HanBaoLuu") != null)
                        ////{
                        string s = gvHocVien.GetFocusedRowCellValue("SoTuanBL").ToString();
                        ngaybl = dtp = DateTime.Parse(e.Value.ToString());
                        
                        if (s == "")
                            return;
                        songay = int.Parse(s);
                        //songay = int.Parse(Config.GetValue("HanBaoLuu").ToString());
                        dtp = DateTime.Parse(e.Value.ToString());
                        dtp = dtp.AddDays(songay * 7);
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayHH"], dtp);
                    }
                    //tiền bảo lưu
                    if (gvHocVien.GetFocusedRowCellValue("NgayDK").ToString() != "" && e.Value.ToString() != "" && gvHocVien.GetFocusedRowCellValue("HVID").ToString() != "")
                    {
                        decimal sotien = TienBL(DateTime.Parse(gvHocVien.GetFocusedRowCellValue("NgayDK").ToString()), DateTime.Parse(e.Value.ToString()), gvHocVien.GetFocusedRowCellValue("HVID").ToString(), gvHocVien.GetFocusedRowCellValue("MaLop").ToString());
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["BLSoTien"], sotien);
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["SoBuoiBL"], sobuoiCL);
                        gvHocVien.SetFocusedRowCellValue(gvHocVien.Columns["NgayDKBL"], DateTime.Today.ToString());
                    }

                }
        }

        private void grdmalop_EditValueChanged(object sender, EventArgs e)
        {
            // hiển thị học viên theo lớp
            
            string sql = "SELECT * FROM MTDK WHERE malop ='" + grdmalop.EditValue.ToString() + "'" + " and Duyet = 1 and isnghihoc = 0 and MaCNHoc = '" + Config.GetValue("MaCN").ToString() + "'";
            if (!showAll.Checked)
            {
                sql += " and isbl = 0";
            }
            DataTable dthv = new DataTable();
            dthv = db.GetDataTable(sql);
            gcHocVien.DataSource = dthv;
            //gvHocVien.BestFitColumns();
            if (dthv.Rows.Count == 0)
            {
                XtraMessageBox.Show("Không có học viên nào đăng ký lớp này", Config.GetValue("PackageName").ToString());
                return ;            
            }
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            if (gvHocVien.DataRowCount == 0 || gvHocVien.FocusedRowHandle < 0)
            {
                XtraMessageBox.Show("Cần chọn học viên bảo lưu để in phiếu",
                    Config.GetValue("PackageName").ToString(),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            DataRow drCur = gvHocVien.GetDataRow(gvHocVien.FocusedRowHandle);
            if (drCur.RowState != DataRowState.Unchanged || !Convert.ToBoolean(drCur["IsBL"]))
            {
                XtraMessageBox.Show("Đang chọn học viên " + drCur["TenHV"] + "\nCần bảo lưu và lưu số liệu trước khi in phiếu",
                    Config.GetValue("PackageName").ToString(),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            Config.NewKeyValue("@HVID", drCur["HVID"]);

            ReportPreview rpDSHV = FormFactory.FormFactory.Create(FormType.Report, "1555") as ReportPreview;
            if (rpDSHV == null)
                return;
            rpDSHV.StartPosition = FormStartPosition.CenterScreen;
            rpDSHV.ShowDialog();
        }

    }
}