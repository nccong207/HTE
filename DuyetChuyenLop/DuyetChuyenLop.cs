using System;
using System.Collections.Generic;
using System.Text;
using Plugins;
using System.Data;
using DevExpress.XtraEditors;
using CDTLib;
using System.Windows.Forms;

namespace DuyetChuyenLop
{
    public class DuyetChuyenLop:ICData
    {

        #region ICData Members
        private DataCustomData _data;
        private InfoCustomData _info = new InfoCustomData(IDataType.MasterDetailDt); 

        public DataCustomData Data
        {
            set { _data = value; }
        }

        public void ExecuteAfter()
        {
            
        }

        public void ExecuteBefore()
        {
            Duyet();
        }

        public InfoCustomData Info
        {
            get { return _info; }
        }

        #endregion

        #region Chức năng duyệt

        private void Duyet()
        {
            if (_data.CurMasterIndex < 0)
                return;
            DataRow drCur = _data.DsData.Tables[0].Rows[_data.CurMasterIndex];
            if (drCur == null)
                return;

            string msg = "";
            string duyet = "3.Duyệt";
            string trinhDuyet = "2.Trình duyệt";
            string chuaDuyet = "1.Chưa duyệt";

            if (drCur.RowState == DataRowState.Deleted)
            {
                if (!drCur["CoDuyet", DataRowVersion.Original].ToString().Equals("1.Chưa duyệt"))
                    msg = "Phiếu đã " + drCur["CoDuyet", DataRowVersion.Original].ToString() + " không được xóa!";
            }

            if (drCur.RowState == DataRowState.Added)
            {
                if (drCur["CoDuyet"].ToString().Equals("3.Duyệt"))
                    msg = "Phiếu tạo mới phải là chưa duyệt hoặc trình duyệt!";
            }

            if (drCur.RowState == DataRowState.Modified)
            {
                string duyetOri1 = drCur["CoDuyet", DataRowVersion.Original].ToString();
                string duyetCur1 = drCur["CoDuyet", DataRowVersion.Current].ToString();

                if (duyetOri1 == chuaDuyet && duyetCur1 == duyet)
                    msg = "Phiếu phải được trình duyệt trước khi duyệt.";

                if (duyetOri1 == trinhDuyet
                        || duyetOri1 == duyet)
                {
                    if (!Boolean.Parse(Config.GetValue("Admin").ToString()) && !Boolean.Parse(_data.DrTable["sApprove"].ToString()))
                    {
                        msg = "Không có quyền thực hiện chức năng";
                    }

                    if (duyetOri1 == duyet)
                        msg = "Phiếu đã duyệt không được sửa!";

                    if (duyetOri1 != duyetCur1)
                    {
                        List<string> lstField = new List<string>(new string[] { "NgayCL", "MaNLop", "MaCNSau", "NgayHocLai", "MaLopSau", "GhiChu" });
                        foreach (string i in lstField)
                        {
                            if (!drCur[i, DataRowVersion.Original].ToString().Equals(drCur[i, DataRowVersion.Current].ToString()))
                            {
                                msg = "Phiếu đã " + duyetOri1 + " không được sửa!";
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (duyetCur1 == trinhDuyet
                                || duyetCur1 == duyet)
                            msg = "Phiếu đã " + duyetCur1 + " không được sửa!";
                    }
                }
            }

            if (msg != "")
            {
                XtraMessageBox.Show(msg, Config.GetValue("PackageName").ToString());
                _info.Result = false;
                return;
            }

            if (drCur.RowState == DataRowState.Modified || drCur.RowState == DataRowState.Added)
            {
                string duyetOri = drCur.RowState == DataRowState.Added?"":drCur["CoDuyet", DataRowVersion.Original].ToString();
                string duyetCur = drCur["CoDuyet", DataRowVersion.Current].ToString();
                if (duyetOri != duyetCur)
                    XuLyDuyet(drCur);
            }
        }

        //Insert tình trạng và cập nhập tình trạng
        private void InsertTinhTrang(string hvtvid,int tinhTrangID,string refMTID,string mode)
        {
            string manv = Config.GetValue("UserName").ToString();
            string tablename = _data.DrTableMaster["TableName"].ToString();

            string[] paraName = new string[]{"@hvid","refmtid","reftable","@tinhtrangid","@manv","@mode"};
            object[] paraObj = new object[]{hvtvid,refMTID,tablename,tinhTrangID,manv,mode};
            _data.DbData.UpdateDatabyStore("UpdateTinhTrang", paraName, paraObj);
        }

        //Insert thông tin lớp sau vào mtdk
        private void InsertMTDK(DataRow drCur)
        {
            drCur["NguoiDuyet"] = Config.GetValue("UserName");
            DateTime ngayHocLai = Convert.ToDateTime(drCur["NgayHocLai", DataRowVersion.Current]);
            string malopcu = drCur["MaLopHT", DataRowVersion.Current].ToString();
            string malopsau = drCur["MaLopSau",DataRowVersion.Current].ToString();
            string manlop = drCur["MaNLop",DataRowVersion.Current].ToString();
            string cnsau = drCur["MaCNSau",DataRowVersion.Current].ToString();
            string cntruoc = drCur["MaCNHT",DataRowVersion.Current].ToString();
            string hvtvid = drCur["TVID",DataRowVersion.Current].ToString();
            string mahv = drCur["MaHV",DataRowVersion.Current].ToString();
            DateTime ngayCL = Convert.ToDateTime(drCur["NgayCL"]);
            object oSBDH = _data.DbData.GetValue("select SoBuoiDH from MTDK where HVID = '" + mahv + "'");
            DateTime ngayKT = TinhNgayKT(malopsau, ngayHocLai, Convert.ToInt32(oSBDH));
            string sql = string.Format(@"
                            UPDATE MTDK SET ISNGHIHOC = 1 , NGAYNGHI = '{8}', GhiChu = N'Chuyển sang lớp {7}' WHERE HVID='{5}'
                            IF NOT EXISTS (SELECT HVID FROM MTDK WHERE HVTVID = {6} AND MALOP = '{7}')
                            BEGIN
                                INSERT INTO MTDK(HVID,REFDTDKLOP,MAHVTV,TenHV,HVTVID,NgayTN,GhiChu,SoBuoiDH,ThucThu,ConLai,SoBuoiCL,TTienGT
                                            ,NgayDK,MALOP,MANHOMLOP,MaCNHoc,MaCNDK,NGAYSINH,DUYET,NGUOIDUYET,NgayHocCuoi,TienHP,GiamHP,TongHP)
                                         SELECT NEWID(),REFDTDKLOP,MAHVTV,TenHV,HVTVID,'{10}',N'Chuyển từ lớp {12}',SoBuoiDH,ThucThu,ConLai,SoBuoiCL,TTienGT
                                            ,'{0}','{1}','{2}','{3}','{4}',NGAYSINH,1,'{9}','{11}',TienHP,GiamHP,TongHP
                                         FROM MTDK dk, DMLopHoc l WHERE HVID='{5}' and l.MaLop = '{1}'
                            END
                            --ELSE                 
                            --    UPDATE MTDK SET ISNGHIHOC = 0 , NGAYNGHI = NULL, GhiChu = NULL WHERE HVTVID = {6} AND MALOP = '{7}'
                            UPDATE DMLopHoc set SiSo = SiSo - 1 where MaLop = '{12}'
                            UPDATE DMLopHoc set SiSo = SiSo + 1 where MaLop = '{7}'
                            ", ngayHocLai,malopsau,manlop,cnsau,cntruoc,mahv,hvtvid,malopsau,ngayCL,Config.GetValue("UserName"),drCur["NgayDK"],ngayKT, malopcu);
            _data.DbData.UpdateByNonQuery(sql);
        }
        // ngày hết học phí
        private DateTime TinhNgayKT(string MaLop, DateTime NgayBD, int SoBuoic)
        {
            DataTable dt = _data.DbData.GetDataTable(string.Format("exec TinhNgayKT '{0}','{1}', '{2}'", SoBuoic, NgayBD, MaLop));
            // tính theo số buổi được học của học viên khi đóng tiền
            if (dt.Rows.Count == 0 || dt.Rows[0]["NgayKT"] == DBNull.Value)
            {
                return DateTime.MinValue;
            }
            DateTime NgayKT = DateTime.Parse(dt.Rows[0]["NgayKT"].ToString());

            return NgayKT;
        }
        #region Tạm thời chưa dùng đến học phí
        //Tính số tháng còn lại 
        private int ThangCL(string hvtvid, string malop ,DateTime ngayNghi)
        {
            int thang = 0;
            string sql = @"select top 1 dk.*,m.ngaydk [ngayhoc]
                                 ,(select sum(sothang) from dtdklop where hvtvid = dk.hvtvid and malop = dk.malop) [tongthang]
                            from dtdklop dk inner join mtdk m on dk.hvtvid = m.hvtvid and dk.malop = m.malop
                            where dk.hvtvid = {0} and dk.malop = '{1}' order by dk.ngaydk desc";
            sql = string.Format(sql, hvtvid, malop);
            using (DataTable dt = _data.DbData.GetDataTable(sql))
            {
                if (dt == null)
                    return 0;
                DataRow dr = dt.Rows[0];
                DateTime ngayDK = Convert.ToDateTime(dr["ngayhoc"]);
                thang = Convert.ToInt32(dr["tongthang"]) - ((ngayNghi.Month - ngayDK.Month) + 12 * (ngayNghi.Year - ngayDK.Year));
            }
            return thang;      
        }

        //Tính số buổi còn lại
        private int SoBuoiCL(string hvtvid, string malop, DateTime ngaynghi)
        {
            int sbhoc = 0; int sbdahoc = 0; int sbcl = 0;
            string sql = @" select top 1 dk.*,m.ngaydk [ngayhoc]
                            ,(select sum(sobuoi) from dtdklop where hvtvid = dk.hvtvid and malop = dk.malop) [tongbuoi]
                            from dtdklop dk inner join mtdk m on dk.hvtvid = m.hvtvid and dk.malop = m.malop
                            where dk.hvtvid = {0} and dk.malop = '{1}' order by dk.ngaydk desc";
            string sql1 = @"select	count(id)
		                    from	templichhoc lh 
		                    where	malop = '{0}'
				                    and magio = (select magiohoc from dmlophoc where malop = '{0}')
				                    and ngay between '{1}' and '{2}'";
            sql = string.Format(sql, hvtvid, malop);
            using (DataTable dt = _data.DbData.GetDataTable(sql))
            {
                if (dt == null)
                    return 0;
                DataRow dr = dt.Rows[0];
                sql1 = string.Format(sql1, malop, dr["ngayhoc"], ngaynghi);
                object obj = _data.DbData.GetValue(sql1);
                sbhoc = Convert.ToInt32(dr["tongbuoi"]);
                sbdahoc = Convert.ToInt32(obj);
                sbcl = sbhoc - sbdahoc;
            }
            return sbcl;
        }
        #endregion
        //Xử lý duyệt
        private void XuLyDuyet(DataRow drCur)
        {
            string duyetCur = drCur["CoDuyet",DataRowVersion.Current].ToString();           
            string hvtvid = drCur["TVID",DataRowVersion.Current].ToString();
            string hvclid = drCur["HVCLID",DataRowVersion.Current].ToString();
            string hvclidOri = "";
            string duyetOri = "";
            string hvtvidOri = "";
            if (drCur.RowState == DataRowState.Modified)
            {
                hvclidOri = drCur["HVCLID", DataRowVersion.Original].ToString();
                duyetOri = drCur["CoDuyet", DataRowVersion.Original].ToString();
                hvtvidOri = drCur["TVID", DataRowVersion.Original].ToString();
            }

            if((duyetOri == "1.Chưa duyệt" && duyetCur == "2.Trình duyệt" )
                    || (duyetOri == "" && duyetCur == "2.Trình duyệt"))
            {
                // 3 tình trạng đang chờ chuyển lớp
                InsertTinhTrang(hvtvid,3,hvclid,"ADD");
            }
            if(duyetOri == "2.Trình duyệt" && duyetCur == "1.Chưa duyệt")
            {
                InsertTinhTrang(hvtvidOri,3,hvclidOri,"DELETE");
            }
            if(duyetOri == "2.Trình duyệt" && duyetCur == "3.Duyệt")
            {
                InsertMTDK(drCur);
                InsertTinhTrang(hvtvid, 4, hvclid, "ADD");
            }       
        }

        #endregion
    }
}
