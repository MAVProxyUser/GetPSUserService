using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Web;
using System.Web.Services;
using System.Xml;
using System.IO;
using System.Timers;
using OrBitADCService;
using PSSysPostLibrary;

namespace GetPSUserService
{
    /// <summary>
    /// Service1 的摘要说明
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    public class Service1 : System.Web.Services.WebService
    {
        string WCFUrl = "http://mes.djicorp.com/browserWCFService/DataService.svc";
        string WCFUrlTest = "http://10.13.1.59/browserWCFService_dev1/DataService.svc";
        string PSUrl = "", InvokeUrl_Gw = "", SoapAction = "";
        ADCService adc = new ADCService();
        string xml = "";
        Timer t = new Timer();
        [WebMethod]
        public string GetPSUser()
        {
            //double ms = Interval * 60 * 60 * 100;//
            //if (isStart==1)
            //{
            //    t.Enabled = false;
            //    t.Interval = Interval;
            //    t.Elapsed += t_Elapsed;
            //    t.Enabled = true;
            //}
            //else
            //{
            //    t.Enabled = false;
            //    t.Stop();
            //}
            //t.Start();
            DataSet ds = adc.GetDataSetWithSQLString(WCFUrlTest, @"SELECT MesInterfaceParameterName,MesInterfaceParameterValue FROM dbo.MesInterfaceInfo
                                                            JOIN dbo.MesInterfaceInParameterInfo ON MesInterfaceInParameterInfo.MesInterfaceInfoId = MesInterfaceInfo.MesInterfaceInfoId
                                                            WHERE InterfaceName='GetPSUserService' AND MesInterfaceInfo.isdel=0 AND MesInterfaceInParameterInfo.isdel=0");
            if (ds != null && ds.Tables.Count > 0 && ds.Tables[ds.Tables.Count - 1].Rows.Count > 0)
            {
                for (int i = 0; i < ds.Tables[ds.Tables.Count - 1].Rows.Count; i++)
                {
                    if (ds.Tables[ds.Tables.Count - 1].Rows[i]["MesInterfaceParameterName"].ToString() == "PSUrlTest")
                        PSUrl = ds.Tables[ds.Tables.Count - 1].Rows[i]["MesInterfaceParameterValue"].ToString();

                    if (ds.Tables[ds.Tables.Count - 1].Rows[i]["MesInterfaceParameterName"].ToString() == "InvokeUrl_Gw")
                        InvokeUrl_Gw = ds.Tables[ds.Tables.Count - 1].Rows[i]["MesInterfaceParameterValue"].ToString();

                    if (ds.Tables[ds.Tables.Count - 1].Rows[i]["MesInterfaceParameterName"].ToString() == "SoapAction")
                        SoapAction = ds.Tables[ds.Tables.Count - 1].Rows[i]["MesInterfaceParameterValue"].ToString();

                    if (ds.Tables[ds.Tables.Count - 1].Rows[i]["MesInterfaceParameterName"].ToString() == "body")
                        xml = ds.Tables[ds.Tables.Count - 1].Rows[i]["MesInterfaceParameterValue"].ToString();
                }
            }
            if (PSUrl == "" || InvokeUrl_Gw == "" || SoapAction == "" || xml == "")
                return "";
            PSSysPost pp = new PSSysPost(InvokeUrl_Gw, "MES", PSUrl);
            pp.init();
            pp.SetHttpHeaders("SoapAction", SoapAction);
            string retXML = pp.Request(string.Format(xml, ""));
            Debug.Write(retXML);
            AnalysisXML(retXML);

            return "";
        }

        void t_Elapsed(object sender, ElapsedEventArgs e)
        {
            t.Stop();
           
            PSSysPost pp = new PSSysPost("PS.DJI_GETEMPFTNPAY.post", "MES", "https://gw-sanbox.djicorp.com/api/PS.DJI_GETEMPFTNPAY.post");
            pp.init();
            pp.SetHttpHeaders("SoapAction", "DJI_GETEMPFTNPAY.v1");
            string retXML = pp.Request(string.Format(xml, ""));
            Debug.Write(retXML);
            AnalysisXML(retXML);
            t.Start();
        }


        /// <summary>
        /// 解析数据并保存
        /// </summary>
        /// <param name="xml">参数</param>
        /// <param name="EMPLID">工号</param>
        void AnalysisXML(string xml, string EMPLID = "")
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xml);
            XmlNodeList rootNode = xmlDoc.GetElementsByTagName("Transaction"); //获取主要数据节点
            int isupdateCheck = 0; //是否只更新一个
            int icount = 0, IsDel = 0;
            DataTable result = new DataTable();
            DataRow newRow;
            string sql = @"EXEC UpdatePSSysUserInfo
                                    @IsDel ={0},
                                    @struser='{1}',
                                    @EMPLID='{2}'";
            DataSet ds = new DataSet();
            result.Columns.Add("EMPLID", "EMPLID".GetType());
            result.Columns.Add("NAME", "NAME".GetType());
            result.Columns.Add("HIRE_DT", "HIRE_DT".GetType());
            if (rootNode != null)
                for (int i = 0; i < rootNode.Count; i++)
                {
                    newRow = result.NewRow();
                    Debug.WriteLine("----------------------------" + ++icount);
                    for (int j = 0; j < rootNode[i].ChildNodes[0].ChildNodes.Count; j++)
                    {
                        Debug.WriteLine(rootNode[i].ChildNodes[0].ChildNodes[j].Name + ":" + rootNode[i].ChildNodes[0].ChildNodes[j].InnerText);
                        if (rootNode[i].ChildNodes[0].ChildNodes[j].Name == "EMPLID")
                            newRow["EMPLID"] = rootNode[i].ChildNodes[0].ChildNodes[j].InnerText;
                        if (rootNode[i].ChildNodes[0].ChildNodes[j].Name == "NAME")
                            newRow["NAME"] = rootNode[i].ChildNodes[0].ChildNodes[j].InnerText;
                        if (rootNode[i].ChildNodes[0].ChildNodes[j].Name == "HIRE_DT")
                            newRow["HIRE_DT"] = rootNode[i].ChildNodes[0].ChildNodes[j].InnerText;
                    }
                    result.Rows.Add(newRow);
                    if (i > 0 && i % 100 == 0)
                    {
                        if (EMPLID == "" && isupdateCheck == 0) //工号不为空第一次调用
                            IsDel = isupdateCheck = 1; //传参数清数据
                        ds.Tables.Add(result);
                        string strxml = ds.GetXml();
                        result.Rows.Clear();
                        ds.Tables.Clear();
                        adc.GetDataSetWithSQLString(WCFUrlTest, string.Format(sql, IsDel, strxml, EMPLID));
                        IsDel = 0;
                    }
                }

            if (result.Rows.Count > 0)
            {
                if (EMPLID == "" && isupdateCheck == 0)
                    IsDel = isupdateCheck = 1;

                ds.Tables.Add(result);
                string strxml = ds.GetXml();
                ds.Tables.Clear();
                adc.GetDataSetWithSQLString(WCFUrlTest, string.Format(sql, IsDel, strxml, EMPLID));
                IsDel = 0;
            }

        }
    }
}