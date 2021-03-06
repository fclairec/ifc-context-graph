﻿using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QL4BIMspatial;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using Xbim.Ifc;
using Microsoft.Practices.Unity;
///using Xbim.Ifc2x3.Interfaces;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;
using Xbim.ModelGeometry.Scene;
using Xbim.ModelGeometry.Scene.Extensions;
using Xbim.Presentation;
using buildingcomponents;
using CsvHelper;





namespace Test
{
    class Program
    {

        private static UnityContainer _container = new UnityContainer();
        private static MainInterface _ql4Spatial = new MainInterface(_container);
       


        public static void Main(string[] args)
        {
            string fileName = args[0];
            string[] files = Directory.GetFiles("C:\\Projekte\\ContextGraph\\input", "*.ifc");
            int tot_files = files.Count();
            Console.WriteLine("Total files:");
            Console.WriteLine(tot_files);

            // Entities to consider
            List<string> MyEntityTypes = new List<string>(new string[] { "IfcWall", "IfcSlab", "IfcFurnishingElement", "IfcColumn",
                "IfcDistributionControlElement", "IfcStair", "IfcWallStandardCase", "IfcFlowTerminal", "IfcFlowSegment", "IfcFlowFitting",
            "IfcFlowController", "IfcDoor", "IfcWindow"});



            try
            {
                // Loop through all files in DIR
                
                    var MyTriangles = new Dictionary<int, Triangles>();
                    var MyBuildingComponents = new Dictionary<int, BuildingComponents>();

                    string temp = Path.GetFileNameWithoutExtension(fileName);
                    string temp2 = Path.GetDirectoryName(fileName);
                    string outputDir = Path.Combine(temp2, temp) + "neighbourhoods.csv";
                    string outputGraph = Path.Combine(temp2, temp) + "contextgraph.xml";
                //if (File.Exists(outputDir)) continue;
                Console.WriteLine("Processing file:");
                    Console.WriteLine(temp);

                    try
                    {
                        IfcStore model = IfcStore.Open(fileName);
                        Xbim3DModelContext context = new Xbim3DModelContext(model);
                        context.CreateContext();

                        var instances = context.ShapeInstances();

                        XbimMatrix3D wcsTransformation = new XbimMatrix3D();

                        // TEMP VARS                    
                        int bidIntern = 0;
                        int triangleId = 0;
                        long max = model.Instances.CountOf<IIfcElement>();
                        Console.WriteLine(max);

                        foreach (IIfcElement ifcElement in model.Instances.OfType<IIfcElement>())
                        {
                            // Filter only the types wanted
                            var a = ifcElement.GetType().Name;
                            if (!MyEntityTypes.Contains(a)) continue;
                            bidIntern++;

                            IEnumerable<Triangles> myTriangles = WriteTriangles(ifcElement, context, bidIntern, wcsTransformation, model.ModelFactors.OneMeter);
                            double myArea = 0.00;

                            var tot_trinagles = myTriangles.Count();
                            //Console.WriteLine(tot_trinagles);
                            foreach (Triangles triangle in myTriangles)
                            {
                                triangleId++;
                                if (triangle.Area != null) myArea += (double)triangle.Area;
                                MyTriangles.Add(triangleId, triangle);
                            }

                            IfcIdentifier myIfcTag = ifcElement.Tag.GetValueOrDefault();
                            int myTag = bidIntern;


                            BuildingComponents thisComponent = new BuildingComponents()
                            {
                                Id = bidIntern,
                                Tag = myTag,
                                Globalid = ifcElement.GlobalId,
                                Objectname = ifcElement.Name,
                                Objecttype = ifcElement.ObjectType,
                                ElementType = ifcElement.GetType().Name.ToString(),
                                //Storey = mystorey,
                                //Material = mat,
                                //Loadbearing = loadbearing,
                                //Area = myArea,
                                //Weight = myQuantities.Item1,
                                //Volume = myQuantities.Item2
                            };
                            MyBuildingComponents.Add(bidIntern, thisComponent);




                        }
                    //Console.WriteLine(MyBuildingComponents);

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<graphml xmlns=\"http://graphml.graphdrawing.org/xmlns\">");
                    //sb.AppendLine("<key id=\"V-~VDLabel\" for=\"node\" attr.name=\"~VDLabel\" attr.type=\"string\" />\r\n  <key id=\"V-~VDRadius\" for=\"node\" attr.name=\"~VDRadius\" attr.type=\"string\" />\r\n  <key id=\"E-~EDLabel\" for=\"edge\" attr.name=\"~EDLabel\" attr.type=\"string\" />");
                    sb.AppendLine("<graph edgedefault=\"directed\">");



                    List<TriangleMesh> resList = new List<TriangleMesh>();
                        foreach (KeyValuePair<int, BuildingComponents> component in MyBuildingComponents)
                        {
                            IEnumerable<KeyValuePair<int, Triangles>> triangles = MyTriangles.Where(t => t.Value.ComponentId == component.Key).ToList();
                            if (!triangles.Any())
                            {
                                continue;
                            }

                            List<Tuple<double, double, double>> vertices = new List<Tuple<double, double, double>>();


                            foreach (KeyValuePair<int, Triangles> triangle in triangles)
                            {
                                Point3D vertex1 = triangle.Value.Point1;
                                Point3D vertex2 = triangle.Value.Point2;
                                Point3D vertex3 = triangle.Value.Point3;
                                vertices.Add(new Tuple<double, double, double>(vertex1.X, vertex1.Y, vertex1.Z));
                                vertices.Add(new Tuple<double, double, double>(vertex2.X, vertex2.Y, vertex2.Z));
                                vertices.Add(new Tuple<double, double, double>(vertex3.X, vertex3.Y, vertex3.Z));
                            }

                            List<Tuple<double, double, double>> distVerts = vertices.Distinct().ToList();

                            List<Tuple<int, int, int>> indices = new List<Tuple<int, int, int>>();


                            foreach (KeyValuePair<int, Triangles> triangle in triangles)
                            {
                                Point3D vertex1 = triangle.Value.Point1;
                                Point3D vertex2 = triangle.Value.Point2;
                                Point3D vertex3 = triangle.Value.Point3;

                                Tuple<double, double, double> myv1 = new Tuple<double, double, double>(vertex1.X, vertex1.Y, vertex1.Z);
                                Tuple<double, double, double> myv2 = new Tuple<double, double, double>(vertex2.X, vertex2.Y, vertex2.Z);
                                Tuple<double, double, double> myv3 = new Tuple<double, double, double>(vertex3.X, vertex3.Y, vertex3.Z);

                                int i1 = 0;
                                int i2 = 0;
                                int i3 = 0;
                                for (int index = 0; index < distVerts.Count; index++)
                                {
                                    Tuple<double, double, double> distVert = distVerts[index];
                                    if (Equals(distVert, myv1))
                                        i1 = index;
                                    if (Equals(distVert, myv2))
                                        i2 = index;
                                    if (Equals(distVert, myv3))
                                        i3 = index;
                                }

                                indices.Add(new Tuple<int, int, int>(i1, i2, i3));
                            }

                            IndexedFaceSet fs = new IndexedFaceSet(distVerts.ToArray(), indices.ToArray(), component.Value.ElementType + "_" + component.Value.Globalid, component.Value.Tag);

                            resList.Add(fs.CreateMesh());

                        }

                        Console.WriteLine("Starting spatial queries after tessalation");
                        // Init the QL4BIM framework
                        _ql4Spatial.GetSettings();
                        // Init the Settings for the operators
                        ISettings settings = _container.Resolve<ISettings>();
                        settings.Touch.PositiveOffset = 0.05;
                        settings.Touch.NegativeOffsetAsRatio = 1;
                        settings.Direction.RaysPerSquareMeter = 10;
                        settings.Direction.PositiveOffset = 0.1;

                        // Init the Operator 
                        IDirectionalOperators directionalOp = _container.Resolve<IDirectionalOperators>();
                        ITouchOperator touchingOp = _container.Resolve<ITouchOperator>();

                        StringBuilder edgestrings = new StringBuilder();

                        foreach (TriangleMesh item1 in resList)
                        {
                            // define edge for every item
                            //sb.AppendFormat("<node id=\"{0}\">\r\n<data key=\"V-~VDLabel\">{0}</data>\r\n<data key=\"V-~VDRadius\">5</data>\r\n</node>" + Environment.NewLine, item1.Name);
                            sb.AppendFormat("<node id=\"{0}\" />" + Environment.NewLine, item1.Name);


                            //loop over all elements again
                            foreach (TriangleMesh item2 in resList)
                            {
                            bool touchingelement = false;
                            if (item1 == item2) continue;
                            try
                            {
                                touchingelement = touchingOp.Touch(item1, item2);
                            }
                            catch (Exception)
                            {
                                continue;
                            }

                            if (!touchingelement) continue;

                            if (directionalOp.AboveOfStrict(item1, item2))
                            {
                                edgestrings.AppendFormat("<edge id=\"{0}\" source=\"{1}\" target=\"{2}\" />" + Environment.NewLine, "e" + item1.Name + item2.Name, item1.Name, item2.Name);
                            }
                            if (directionalOp.BelowOfStrict(item1, item2))
                            {
                                edgestrings.AppendFormat("<edge id=\"{0}\" source=\"{1}\" target=\"{2}\" />" + Environment.NewLine, "e" + item2.Name + item1.Name, item2.Name, item1.Name);
                            }


                            var nameOfIntersect = item2.ToString().Split(new[] { "_" }, StringSplitOptions.None)[0];
                            WriteSpaceRow(item1.ToString(), outputDir, nameOfIntersect);

                            }

                        }
                        Console.WriteLine("Reached end of spatial intersection search");

                        sb.AppendLine(edgestrings.ToString());
                        sb.AppendLine("</graph>\r\n</graphml>");
                        File.WriteAllText(outputGraph, sb.ToString());
                        Console.WriteLine("XML written?");




                }

                catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                    }
                    

                    
                
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
            System.Console.ReadLine();
        }

        private static IEnumerable<Triangles> WriteTriangles(IIfcProduct ifcElement, Xbim3DModelContext context, int bidIntern, XbimMatrix3D wcsTransformation, double modelFactorsOneMeter)
        {
            List<Triangles> allTriangles = new List<Triangles>();

            foreach (XbimShapeInstance instance in context.ShapeInstancesOf(ifcElement).Where(x => x.RepresentationType == XbimGeometryRepresentationType.OpeningsAndAdditionsIncluded))
            {
                //Console.WriteLine(instance.IfcProductLabel);
                XbimShapeGeometry geometry = context.ShapeGeometry(instance);
                byte[] data = ((IXbimShapeGeometryData)geometry).ShapeData;
                using (MemoryStream stream = new MemoryStream(data))
                {
                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        XbimShapeTriangulation mesh = reader.ReadShapeTriangulation();
                        mesh = mesh.Transform(instance.Transformation);
                        // WCS transform
                        mesh = mesh.Transform(wcsTransformation);


                        foreach (XbimFaceTriangulation face in mesh.Faces)
                        {
                            int j = 0;
                            for (int i = 0; i < face.TriangleCount; i++)
                            {
                                int k = i + j;
                                Point3D point1 = new Point3D { X = mesh.Vertices[face.Indices[k]].X / modelFactorsOneMeter, Y = mesh.Vertices[face.Indices[k]].Y / modelFactorsOneMeter, Z = mesh.Vertices[face.Indices[k]].Z / modelFactorsOneMeter };
                                j++;
                                k = i + j;
                                Point3D point2 = new Point3D { X = mesh.Vertices[face.Indices[k]].X / modelFactorsOneMeter, Y = mesh.Vertices[face.Indices[k]].Y / modelFactorsOneMeter, Z = mesh.Vertices[face.Indices[k]].Z / modelFactorsOneMeter };
                                j++;
                                k = i + j;
                                Point3D point3 = new Point3D { X = mesh.Vertices[face.Indices[k]].X / modelFactorsOneMeter, Y = mesh.Vertices[face.Indices[k]].Y / modelFactorsOneMeter, Z = mesh.Vertices[face.Indices[k]].Z / modelFactorsOneMeter };
                                double a =
                                    Math.Sqrt(Math.Pow(point2.X - point1.X, 2) + Math.Pow(point2.Y - point1.Y, 2) +
                                              Math.Pow(point2.Z - point1.Z, 2));
                                double b =
                                    Math.Sqrt(Math.Pow(point3.X - point2.X, 2) + Math.Pow(point3.Y - point2.Y, 2) +
                                              Math.Pow(point3.Z - point2.Z, 2));
                                double c =
                                    Math.Sqrt(Math.Pow(point1.X - point3.X, 2) + Math.Pow(point1.Y - point3.Y, 2) +
                                              Math.Pow(point1.Z - point3.Z, 2));
                                // SEE HERON'S FORMULA
                                double s = (a + b + c) / 2;
                                double myArea = Math.Sqrt(s * (s - a) * (s - b) * (s - c));
                                Triangles mytriangle = new Triangles { ComponentId = bidIntern, Point1 = point1, Point2 = point2, Point3 = point3, Area = myArea };
                                allTriangles.Add(mytriangle);
                            }
                        }
                    }
                }
            }
            return allTriangles;
        }


        private static void WriteSpaceRow(string entityType, string outputDirectory, string touching_element)
        {
            //var sting = None;
            var isWall = 0;
            var isStair = 0;
            var isSlab = 0;
            var isFurn = 0;
            var isCol = 0;
            var isFlowT = 0;
            var isFlowS = 0;
            var isFlowF = 0;
            var isFlowC = 0;
            var isDist = 0;
            var isWin = 0;
            var isDoor = 0; 
            if (touching_element == "IfcWall" || touching_element == "IfcWallStandardCase") { isWall = 1; }
            if (touching_element == "IfcStair" || touching_element == "IfcStairFlight") { isStair = 1; }
            if (touching_element == "IfcColumn") { isCol = 1; }
            if (touching_element == "IfcSlab") { isSlab = 1; }
            if (touching_element == "IfcFunishingElement") {isWall = 1; }
            if (touching_element == "IfcFlowTerminal" ) { isFlowT = 1; }
            if (touching_element == "IfcFlowSegment") { isFlowS = 1; }
            if (touching_element == "IfcFlowFitting") { isFlowF = 1; }
            if (touching_element == "IfcFlowControler") { isFlowC = 1; }
            if (touching_element == "IfcDistributionControlElement") { isDist = 1; }
            if (touching_element == "IfcWindow") { isWin = 1; }
            if (touching_element == "IfcDoor") { isDoor = 1; }
                                          
            var data = new[]
            {
                new MyNeighbourhood {Entity = entityType, Wall = isWall, Stair = isStair, Slab = isSlab,
                    FurnishingElement = isFurn,
                    Column = isCol, Flowterminal = isFlowT, FlowSegment = isFlowS,
                    FlowFitting = isFlowF, FlowController = isFlowC, DistributionControlElement = isDist,
                    Window = isWin, Door = isDoor }
            };
            //Console.WriteLine(outputDirectory);
            using (var mem = new FileStream(outputDirectory, FileMode.Append, FileAccess.Write))
            using (var writer = new StreamWriter(mem))
            using (var csvWriter = new CsvHelper.CsvWriter(writer, System.Globalization.CultureInfo.CurrentCulture))
            {
                csvWriter.Configuration.Delimiter = ",";
                csvWriter.Configuration.HasHeaderRecord = false;
                csvWriter.Configuration.AutoMap<MyNeighbourhood>();

                //csvWriter.WriteHeader<MyNeighbourhood>();
                csvWriter.WriteRecords(data);
                               

                writer.Flush();
                
                //File.WriteAllText("output.csv", arr.ToString());
            }


        }










    }
    
}
