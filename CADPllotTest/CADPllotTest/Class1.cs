﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using System.Windows.Forms;
using Autodesk.AutoCAD.PlottingServices;
using System.IO;
using ExcelClass;


[assembly: CommandClass(typeof(CADPllotTest.Class1))]
namespace CADPllotTest
{
    public class Class1
    {

        public static double LEN_BOUNDARY;
        public static double LEN_MILE;
        public static double LEN_TEXT ;
        public static double SIZE_TEXT ;
        public static double TIMES;
        public static double TEXT_OFFSET_X;
        public static double TEXT_OFFSET_Y;

        [CommandMethod("e2p")]
        public static void ChangePlotSetting()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Select file";
            dialog.InitialDirectory = ".\\";
            dialog.Filter = "xls files (*.*)|*.xlsx;*.xls;";
            dialog.InitialDirectory = @"~\desktop";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string excelPath = dialog.FileName;
                CADProcess(excelPath);
            }
        }



        private static bool CADProcess(string excelPath)
        { 
            LEN_BOUNDARY = 70000;
            LEN_MILE = 2000;
            LEN_TEXT = 5000;
            SIZE_TEXT = 5000;
            TIMES = 1;
            TEXT_OFFSET_X = 1500;
            TEXT_OFFSET_Y = 12000;
            Document acDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
            Database acCurDb = acDoc.Database;

            PromptStringOptions pStrOpts = new PromptStringOptions("\n 輸入預處理之excel分頁\n單頁 or \n多頁 (1,3,4 or 1-5) :  ");
            pStrOpts.AllowSpaces = true;
            PromptResult pStrRes = acDoc.Editor.GetString(pStrOpts);
            string num = pStrRes.StringResult.Trim();

            pStrOpts = new PromptStringOptions("\n 是否繪製里程(1 : 是 , 0 : 否):  ");
            pStrOpts.AllowSpaces = true;
            pStrRes = acDoc.Editor.GetString(pStrOpts);
            string num2 = pStrRes.StringResult.Trim();

            if (num.Trim() == "") return false;


            int kk = 1;
            List<string[,]> allData = new List<string[,]>();
            List<string> label = new List<string>();
            List<double> angle = new List<double>();
            List<Point[]> milePoints = new List<Point[]>();
            List<Point[]> labelPoints = new List<Point[]>();
            List<Point[]> BoundaryCoordinate = new List<Point[]>();
            bool mileOpen = num2 == "1" ? true : false;
            try
            {
                ed.WriteMessage(LEN_MILE.ToString());
                ed.WriteMessage(num2.ToString());
                allData = ExcelClass.ExcelSaveAndRead.Read(excelPath, 3, 1, num);
                List<List<Point>> PolyData = new List<List<Point>>();
                bool open = true;
                foreach (var data in allData)
                {
                    List<Point> Data = DataProces(data);
                    if (open)
                    {
                        Point3d p1 = new Point3d(Data[0].X, Data[0].Y, 0);
                        Point3d p2 = new Point3d(Data[1].X, Data[1].Y, 0);
                        double Len = (new Line(p1, p2)).Length;
                        open = false;
                        TIMES = Len / 670;
                        LEN_BOUNDARY = LEN_BOUNDARY * TIMES;
                        LEN_MILE = LEN_MILE * TIMES;
                        LEN_TEXT = 3 * LEN_MILE;
                        SIZE_TEXT = SIZE_TEXT * TIMES;
                        TEXT_OFFSET_X = 0;
                        TEXT_OFFSET_Y = 0; // TEXT_OFFSET_Y * TIMES;
                    }

                    //CAD_PLOT(Data, acDoc, ed, acCurDb); 
                    //PolyData.Add(Data);
                    CreatePolyLines(Data, acCurDb);

                    GetMileInformation(Data, ref milePoints, ref labelPoints, ref label, ref angle, mileOpen);
                    GetMileageBoundaryInformation(ref BoundaryCoordinate, ref allData, Data, kk, mileOpen);
                    kk = kk + 1;
                }



                MileageProces(acDoc, ed, acCurDb, milePoints, BoundaryCoordinate, labelPoints, label, angle, mileOpen);
            }
            catch (System.Exception)
            {
                MessageBox.Show("輸入之sheet有誤");
                return false;
            }
            return true;
        }

        private static bool MileageProces(Document acDoc, Editor ed, Database acCurDb,
                                        List<Point[]> milePoints, List<Point[]> BoundaryCoordinate,
                                        List<Point[]> labelPoints, List<string> label, List<double> angle, bool mileOpen)
        {
            if (!mileOpen) return false;


            foreach (Point[] mm in milePoints)
                CAD_PLOT(GetVerticalPoinyByDistancd(LEN_MILE, mm), acDoc, ed, acCurDb);


            foreach (Point[] bb in BoundaryCoordinate)
                CAD_PLOT(GetVerticalPoinyByDistancd(LEN_BOUNDARY, bb), acDoc, ed, acCurDb);

            for (int kk = 0; kk < labelPoints.Count; kk++)
                CAD_Text(acDoc, acCurDb, SIZE_TEXT, TEXT_OFFSET_X, TEXT_OFFSET_Y, GetVerticalPoinyByDistancd(LEN_TEXT, labelPoints[kk]), label[kk], angle[kk]);

            return true;

        }


        private static bool GetMileInformation(List<Point> tmpData, ref List<Point[]> milePoints, ref List<Point[]> labelPoints, ref List<string> label, ref List<double> angle, bool mileOpen)
        {
            if (!mileOpen) return false;

            for (int i = 0; i < tmpData.Count; i++)
            {
                if (tmpData[i].D % 20 == 0)
                {
                    Point[] points = new Point[2];
                    if (i != tmpData.Count - 1)
                    {
                        points[0] = new Point(tmpData[i].D, tmpData[i].X, tmpData[i].Y);
                        points[1] = new Point(tmpData[i + 1].D, tmpData[i + 1].X, tmpData[i + 1].Y);
                    }
                    else
                    {
                        points[0] = new Point(tmpData[i].D, tmpData[i].X, tmpData[i].Y);
                        points[1] = new Point(tmpData[i - 1].D, tmpData[i - 1].X, tmpData[i - 1].Y);
                    }

                    milePoints.Add(points);

                    if (tmpData[i].D % 100 == 0)
                    {
                        string tmp1 = (Math.Floor(tmpData[i].D / 1000)).ToString();
                        string tmp2 = (tmpData[i].D - Math.Floor(tmpData[i].D / 1000) * 1000).ToString("000");
                        label.Add(tmp1 + "K+" + tmp2);
                        //labelPoints.Add(GetTextCoor(num2, points));
                        labelPoints.Add(points);
                        angle.Add(GetAngle(points));
                    }
                }
            }
            return true;
        }

        private static bool GetMileageBoundaryInformation(ref List<Point[]> BoundaryCoordinate, ref List<string[,]> allData, List<Point> Data, int kk, bool mileOpen)
        {
            if (!mileOpen) return false;

            int ll = Data.Count;
            if (kk == 1 && allData.Count == 1)
            {
                BoundaryCoordinate.Add(new Point[2] { new Point(Data[0].D, Data[0].X, Data[0].Y),
                                                              new Point(Data[1].D, Data[1].X, Data[1].Y) });
                BoundaryCoordinate.Add(new Point[2] { new Point(Data[ll - 1].D, Data[ll - 1].X, Data[ll - 1].Y),
                                                              new Point(Data[ll - 2].D, Data[ll - 2].X, Data[ll - 2].Y) });
            }
            else if (kk == allData.Count)
            {
                BoundaryCoordinate.Add(new Point[2] { new Point(Data[ll - 1].D, Data[ll - 1].X, Data[ll - 1].Y),
                                                              new Point(Data[ll - 2].D, Data[ll - 2].X, Data[ll - 2].Y) });
            }
            else if (kk == 1)
            {
                BoundaryCoordinate.Add(new Point[2] { new Point(Data[0].D, Data[0].X, Data[0].Y),
                                                              new Point(Data[1].D, Data[1].X, Data[1].Y) });
            }
            return true;
        }

        private static List<Point> DataProces(string[,] exData)
        {
            double[,] data = new double[exData.GetLength(0), 3];
            List<int> ngPo = new List<int>();
            List<Point> Points = new List<Point>();
            for (int j = 0; j < exData.GetLength(0); j++)
            {
                if (exData[j, 0].Trim() != "" && exData[j, 1].Trim() != "" && exData[j, 2].Trim() != "")
                {
                    Points.Add(new Point(double.Parse(exData[j, 0].Trim()), double.Parse(exData[j, 2].Trim()), double.Parse(exData[j, 1].Trim())));
                }
            }
            return Points;
        }

        private static List<Point> GetVerticalPoinyByDistancd(double D, Point[] Data)
        {
            double resX0, resY0, resX1, resY1;
            double m = (Data[1].Y - Data[0].Y) / (Data[1].X - Data[0].X);
            double tmp = Math.Sqrt(D * D / (m * m + 1));
            resY0 = Data[0].Y + tmp;
            resY1 = Data[0].Y - tmp;
            resX0 = Data[0].X - m * tmp;
            resX1 = Data[0].X + m * tmp;
            List<Point> res = new List<Point>();
            res.Add(new Point(0, resX0, resY0));
            res.Add(new Point(0, resX1, resY1));
            return res;
        }

        private static double GetAngle(Point[] point)
        {
            return Math.Atan((point[1].Y - point[0].Y) / (point[1].X - point[0].X));
        }

        private static Point[] GetTextCoor(Double D, Point[] Data)
        {
            double m = (Data[1].Y - Data[0].Y) / (Data[1].X - Data[0].X);
            double tmp = Math.Sqrt(D * D / (m * m + 1));
            double resX0, resY0, resX1, resY1;
            resY0 = Data[0].Y + tmp;
            resY1 = Data[0].Y - tmp;
            resX0 = Data[0].X + m * tmp;
            resX1 = Data[0].X - m * tmp;
            return new Point[] { new Point(0, resX1, resY1), new Point(0, Data[1].X, Data[1].Y) };

        }

        private static void CAD_PLOT(List<Point> data, Document acDoc, Editor ed, Database acCurDb)
        {
            using (Transaction acTrans = acCurDb.TransactionManager.StartTransaction())
            {
                BlockTable acBlkTbl;
                acBlkTbl = acTrans.GetObject(acCurDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                BlockTableRecord acBlkTblRec;
                acBlkTblRec = acTrans.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                for (int i = 0; i < data.Count - 1; i++)
                {
                    using (Line acLine = new Line(
                        new Point3d(data[i].X, data[i].Y, 0),
                        new Point3d(data[i + 1].X, data[i + 1].Y, 0)))
                    {
                        acBlkTblRec.AppendEntity(acLine);
                        acTrans.AddNewlyCreatedDBObject(acLine, true);
                    }
                }
                acTrans.Commit();
            }
        }

        private static void CreatePolyLines(List<Point> points, Database db)
        {
            using (Transaction transaction = db.TransactionManager.StartTransaction())
            {
                ///////
                BlockTable acBlkTbl;
                acBlkTbl = transaction.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;

                // Open the Block table record Model space for write
                BlockTableRecord acBlkTblRec;
                acBlkTblRec = transaction.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                OpenMode.ForWrite) as BlockTableRecord;

                // Create a polyline with two segments (3 points)
                using (Polyline acPoly = new Polyline())
                {
                    for (int i = 0; i < points.Count; i++)
                    {
                        acPoly.AddVertexAt(0, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
                    }
                    //acPoly.AddVertexAt(0, new Point2d(points[points.Count - 1][1].X, points[points.Count - 1][1].Y), 0, 0, 0);

                    //foreach (Point3d[] pp in points)
                    //{ 
                    //    acPoly.AddVertexAt(0, new Point2d(pp[0][0], pp[0][1]), 0, 0, 0);
                    //}
                    //acPoly.AddVertexAt(0, new Point2d(points[points.Count - 1][1][0], points[points.Count - 1][1][1]), 0, 0, 0);

                    // Add the new object to the block table record and the transaction
                    acBlkTblRec.AppendEntity(acPoly);
                    transaction.AddNewlyCreatedDBObject(acPoly, true);
                    transaction.Commit();
                }
            }
        }




        private static void CAD_Text(Document acDoc, Database acCurDb, double SIZE_TEXT, double TEXT_OFFSET_X, double TEXT_OFFSET_Y, List<Point> point, string text, double angle)
        {
            using (Transaction acTrans = acCurDb.TransactionManager.StartTransaction())
            {
                // Open the Block table for read
                BlockTable acBlkTbl;
                acBlkTbl = acTrans.GetObject(acCurDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                // Open the Block table record Model space for write
                BlockTableRecord acBlkTblRec;
                acBlkTblRec = acTrans.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                // Create a single-line text object
                DBText acText = new DBText();
                acText.SetDatabaseDefaults();
                acText.Position = new Point3d(point[1].X - TEXT_OFFSET_X, point[1].Y - TEXT_OFFSET_Y, 0);
                acText.Height = SIZE_TEXT;
                acText.TextString = text;
                acText.Rotation = angle;
                // Change the oblique angle of the text object to 45 degrees(0.707 in   radians)
                // acText.Oblique = 0.707;
                acBlkTblRec.AppendEntity(acText);
                acTrans.AddNewlyCreatedDBObject(acText, true);
                // Save the changes and dispose of the transaction
                acTrans.Commit();
            }
        }
    }




}
