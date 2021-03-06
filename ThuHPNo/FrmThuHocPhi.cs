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
using SaveDangKyLop;

namespace ThuHPNo
{
    public partial class frmThuHocPhi : DevExpress.XtraEditors.XtraForm
    {
        public DataRow _drvDKLop;
        string mahv;
        string tenhv;
        DataTable dt;
        DateTime ngayThu;
        decimal soTien;
        string httt;
        QuanLyCT quanlyCT;

        public frmThuHocPhi(DataTable _dt,DataRow drvDKLop, DataRow drMaster)
        {
            InitializeComponent();
            dt = _dt;
            _drvDKLop = drvDKLop;
            mahv = drMaster["MaHV"].ToString();
            tenhv = drMaster["TenHV"].ToString();
            quanlyCT = new QuanLyCT(mahv, tenhv, Database.NewDataDatabase());
        }

        Database db = Database.NewDataDatabase();
        private void frmThuHocPhi_Load(object sender, EventArgs e)
        {
            //string sql = @"Select NgayThu,SoTien,HTTT,NguoiThu From CTThuTien where dtdklop = '" + drv["ID"] + "'";
            //dt = db.GetDataTable(sql);
            //DataColumn col = new DataColumn();
            //col.ColumnName = "DTDKLop";
            //dt.Columns.Add(col);
            //col = new DataColumn();
            //col.ColumnName = "MT11ID";
            //dt.Columns.Add(col);
            //col = new DataColumn();
            //col.ColumnName = "MT15ID";
            //dt.Columns.Add(col);
            gridControl1.DataSource = dt;
        }

        private void btThem_Click(object sender, EventArgs e)
        {
            // nếu học viên đã nghỉ học thì không cho phép thu phí tiếp.
            string query = string.Format(@"Select HVID From MTDK Where refDTDKLop = '{0}' and isNghiHoc = 1", _drvDKLop["ID"]);
            if (db.GetDataTable(query).Rows.Count > 0)
            {
                XtraMessageBox.Show("Học viên này đã nghỉ học, không được phép thu phí tiếp !", Config.GetValue("PackageName").ToString());
                return;
            }
            
            FrmHocPhi frmHP = new FrmHocPhi(_drvDKLop);
            frmHP.ShowDialog();
            if (frmHP.DialogResult == DialogResult.OK)
            {
                ngayThu = frmHP.ngaythu;
                soTien = frmHP.sotien;
                httt = frmHP.httt;
                
                //Thêm trong dữ liệu
                if (httt != "" && soTien > 0)
                {
                    bool isInsertMT43 = false;
                    string sql = "";
                    DataRow drvDKLop = _drvDKLop.Table.NewRow();
                    drvDKLop["ID"] = _drvDKLop["ID"];
                    drvDKLop["isMuaBT"] = frmHP.isMuaBT;
                    drvDKLop["TienGT"] = frmHP.isMuaBT ? soTien : 0;
                    drvDKLop["NgayTN"] = ngayThu;
                    drvDKLop["TDaNop"] = soTien;
                    drvDKLop["HTTT"] = httt;
                    // không phải là dịch vụ liên kết
                    if (Config.GetValue("MaCN").ToString() != "DVLK")
                    {
                        // Thanh toán bằng tiền mặt
                        if (httt == "TM")
                        {
                            sql += quanlyCT.GetStringInsertMT11_BLTK(drvDKLop, isInsertMT43);
                        }
                        // Không phải thanh toán bằng tiền mặt
                        else
                        {
                            sql += quanlyCT.GetStringInsertMT15_BLTK(drvDKLop, isInsertMT43);
                        }
                    }
                    // dịch vụ liên kết
                    else
                        sql += quanlyCT.GetStringInsertMT31_BLTK(drvDKLop, isInsertMT43);
                    // thực thi
                    if (db.UpdateByNonQuery(sql))
                    { 
                        //Thêm trên giao diện
                        gridView1.AddNewRow();
                        gridView1.SetFocusedRowCellValue(gridView1.Columns["NgayThu"], ngayThu);
                        gridView1.SetFocusedRowCellValue(gridView1.Columns["SoTien"], soTien);
                        gridView1.SetFocusedRowCellValue(gridView1.Columns["HTTT"], httt);
                        gridView1.SetFocusedRowCellValue(gridView1.Columns["NguoiThu"], Config.GetValue("UserName").ToString());
                        gridView1.SetFocusedRowCellValue(gridView1.Columns["MT11ID"], drvDKLop["MT11ID"]);
                        gridView1.SetFocusedRowCellValue(gridView1.Columns["MT15ID"], drvDKLop["MT15ID"]);
                        gridView1.SetFocusedRowCellValue(gridView1.Columns["DTDKLop"], drvDKLop["ID"].ToString());
                        gridView1.UpdateCurrentRow();
                    }
                }  
            }
        }

        private void btDong_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void btnXoa_Click(object sender, EventArgs e)
        {
            if (gridView1.GetSelectedRows().Length > 0)
            {
                if (XtraMessageBox.Show("Bạn có chắc là muốn xóa phiếu thu này ?", Config.GetValue("PackageName").ToString(), MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    gridView1.DeleteSelectedRows();
                }
            }
        }
        private void frmThuHocPhi_FormClosed(object sender, FormClosedEventArgs e)
        {
            //Kiểm tra và cập nhật DTDKLop và CTThuTien
            string sql = "";
            if (dt == null || dt.Rows.Count == 0)
                return;
            DataTable dtChange = dt.GetChanges();
            if (dtChange != null && dtChange.Rows.Count > 0)
            {
                decimal phainop = Convert.ToDecimal(_drvDKLop["TTien"]);
                decimal danop = Convert.ToDecimal(_drvDKLop["TDaNop"]);
                decimal conlai = Convert.ToDecimal(_drvDKLop["TConLai"]);
                foreach (DataRow dr in dtChange.Rows)
                {
                    if (dr.RowState == DataRowState.Added)
                    {
                        //Cập nhật chi tiết thu tiền
                        sql += GetStringInsertCTThuTien(dr);
                        danop += Convert.ToDecimal(dr["SoTien"]);
                    }
                    if (dr.RowState == DataRowState.Deleted)
                    {
                        // kiểm tra phiếu thu đã duyệt hay chưa, nếu duyệt rồi thì thông báo không cho xóa.
                        if (!KiemTraDuyetPhieuThu(dr))
                            return;
                        // xóa chi tiết thu tiền.
                        DeleteCTThuTien(dr);
                        // tiến hành xóa phiếu thu.
                        quanlyCT.Delete_MT11_MT15_MT43_BLTK_BLVT(dr);
                        // cập nhật lại thực nộp + còn lại
                        danop -= Convert.ToDecimal(dr["SoTien", DataRowVersion.Original]);
                    }
                }
                conlai = phainop - danop;
                _drvDKLop["TDaNop"] = danop;
                _drvDKLop["TConLai"] = conlai;
                //Cập nhập DTDKLop
                sql += GetStringUpdateDTDKLop(_drvDKLop);                
                if (sql != "" && db.UpdateByNonQuery(sql))
                    XtraMessageBox.Show("Cập nhật dữ liệu thành công!",
                        Config.GetValue("PackageName").ToString());
            }
        }

        private string GetStringUpdateDTDKLop(DataRow drv1)
        {
            string sql = "";
            string dtdklopID = drv1["ID"].ToString();
            decimal danop = Convert.ToDecimal(drv1["TDaNop"]);
            decimal conlai = Convert.ToDecimal(drv1["TConLai"]);
            sql = string.Format(@"
                                    UPDATE DTDKLop SET TDaNop = {0},TConLai = {1} where ID = '{2}';"
                                    ,danop,conlai,dtdklopID);
            return sql;
        }

        private void DeleteCTThuTien(DataRow dr)
        {
            string sql = "";
            string DTDKLopID = dr["DTDKLop", DataRowVersion.Original].ToString();
            string MT11ID = dr["MT11ID", DataRowVersion.Original].ToString();
            string MT15ID = dr["MT15ID", DataRowVersion.Original].ToString();
            if (MT11ID != "")
            {
                sql = string.Format(@"
                                    DELETE FROM CTTHUTIEN WHERE DTDKLOP = '{0}' AND MT11ID = '{1}';"
                                    , DTDKLopID, MT11ID);
            }
            if (MT15ID != "")
            {
                sql = string.Format(@"
                                    DELETE FROM CTTHUTIEN WHERE DTDKLOP = '{0}' AND MT15ID = '{1}';"
                                    , DTDKLopID, MT15ID);
            }
            if (sql != "")
                db.UpdateByNonQuery(sql);
        }
        private string GetStringInsertCTThuTien(DataRow drv1)
        {
            string sql = "";
            string DTDKLopID = drv1["DTDKLop"].ToString();
            string MT11ID = drv1["MT11ID"].ToString();
            string MT15ID = drv1["MT15ID"].ToString();
            DateTime NgayThu = Convert.ToDateTime(drv1["NgayThu"]);
            decimal SoTien = Convert.ToDecimal(drv1["SoTien"]);
            string HTTT = drv1["HTTT"].ToString();
            string NguoiThu = drv1["NguoiThu"].ToString();

            if (MT11ID != "")
            {
                sql = string.Format(@"
                                    INSERT INTO CTThuTien(DTDKLop,MT11ID,NgayThu,SoTien,HTTT,NguoiThu)
                                    VALUES('{0}','{1}','{2}',{3},'{4}','{5}');"
                                                   , DTDKLopID, MT11ID, NgayThu, SoTien, HTTT, NguoiThu);
            }
            if (MT15ID != "")
            {
                sql = string.Format(@"
                                    INSERT INTO CTThuTien(DTDKLop,MT15ID,NgayThu,SoTien,HTTT,NguoiThu)
                                    VALUES('{0}','{1}','{2}',{3},'{4}','{5}');"
                                                                  , DTDKLopID, MT15ID, NgayThu, SoTien, HTTT, NguoiThu);
            }
            return sql;
        }
        private bool KiemTraDuyetPhieuThu(DataRow dr)
        {
            string sql = "";
            if (dr["MT11ID", DataRowVersion.Original] != DBNull.Value || dr["MT11ID", DataRowVersion.Original].ToString() != "")
                sql = string.Format("Select * From MT11 Where MT11ID = '{0}' and Duyet = 1", dr["MT11ID", DataRowVersion.Original]);
            if (dr["MT15ID", DataRowVersion.Original] != DBNull.Value || dr["MT15ID", DataRowVersion.Original].ToString() != "")
                sql = string.Format("Select * From MT15 Where MT15ID = '{0}' and Duyet = 1", dr["MT15ID", DataRowVersion.Original]);
            if (sql != "" && db.GetValue(sql) != null)
            {
                XtraMessageBox.Show("Phiếu thu này đã được duyệt, không thể xóa phiếu thu này !", Config.GetValue("PackageName").ToString());
                return false;
            }
            return true;
        }
        private void frmThuHocPhi_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                this.Close();
        }

        
    }
}