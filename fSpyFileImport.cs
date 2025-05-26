using Eto.Drawing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Rhino.UI;
using System;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using Bitmap = Eto.Drawing.Bitmap;
using Color = System.Drawing.Color;


namespace fSpyFileImport
{
    public class fSpyFileImportCommand : Command
    {
        public fSpyFileImportCommand()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static fSpyFileImportCommand Instance { get; private set; }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "fSpyFileImport";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {

            RhinoApp.WriteLine("Start {0} command.", EnglishName);

            string filePath = null;

            if (mode == RunMode.Interactive)
            {
                var ofd = new Eto.Forms.OpenFileDialog
                {
                    MultiSelect = false,
                    Title = "Open",
                    Filters =
                    {
                        new Eto.Forms.FileFilter
                        {
                            Name = "fSpy Project (*.fspy)",
                            Extensions = new[] { "*.fspy" }
                        }
                    },
                    CurrentFilterIndex = 0
                };

                var result = ofd.ShowDialog(RhinoEtoApp.MainWindow);
                if (result != Eto.Forms.DialogResult.Ok)
                    return Result.Cancel;

                filePath = ofd.FileName;
            }
            else
            {
                var gs = new GetString();
                gs.SetCommandPrompt("Name of fSpy Project to open");
                gs.Get();
                if (gs.CommandResult() != Result.Success)
                    return gs.CommandResult();
                filePath = gs.StringResult();
            }

            filePath = filePath.Trim();
            if (string.IsNullOrEmpty(filePath))
                return Result.Nothing;

            if (!File.Exists(filePath))
            {
                RhinoApp.WriteLine("File not found.");
                return Result.Failure;
            }

            try
            {
                var project = ImportFSpyProject(filePath);
                ChangeCameraSettings(doc, project);
                AddImage(doc, project);
            }
            catch (Exception e)
            {
                RhinoApp.WriteLine(e.Message);
                return Result.Failure;
            }

            RhinoApp.WriteLine("Done");
            return Result.Success;
        }

        private void AddImage(RhinoDoc doc, fSpyProject project)
        {
            using (Eto.Drawing.Image img = new Bitmap(project.ImageFilePath))
            {
                int width = img.Width;
                int height = img.Height;

                Console.WriteLine($"Width: {width}px");
                Console.WriteLine($"Height: {height}px");
                var plane = Plane.WorldXY;
                plane.Translate(new Vector3d(img.Width, img.Height, 0));

                var id = doc.Objects.AddPictureFrame(plane, project.ImageFilePath, false, img.Width, img.Height, true, true);
            }
           
        }

        private void ChangeCameraSettings(RhinoDoc doc, fSpyProject project)
        {
            var viewportName = "Perspective";
            var view = doc.Views.GetViewList(true,true).FirstOrDefault(v => v.MainViewport.Name == viewportName);
            if (view == null)
            {
                throw new Exception($"Failed to get viewport: {viewportName}");
            }

            var mat = project.CameraParameters.CameraMatrix;

            var scale = UnitConverter.GetImportToModelScale(project.RefDistanceUnit, doc);
            Point3d location = new Point3d(mat[0, 3] * scale, mat[1, 3] * scale, mat[2, 3] * scale);


            Vector3d forward = -new Vector3d(mat[0, 2], mat[1, 2], mat[2, 2]);
            Point3d target = location + forward;


            Vector3d up = new Vector3d(mat[0, 1], mat[1, 1], mat[2, 1]);

#if DEBUG
            DebugDrawAxes(doc, mat, scale);
#endif




            var vp = view.ActiveViewport;
            vp.SetCameraLocation(location, false);
            vp.SetCameraDirection(target - location, false);
            vp.SetCameraTarget(target, false);
            vp.CameraUp = up;

            // rhino default camera is a 36mm x 24mm film gate camera https://wiki.mcneel.com/rhino/rhinolensing 
            double focalLengthMm = project.CameraParameters.RelativeFocalLength * 36.0;
            vp.ChangeToTwoPointPerspectiveProjection(focalLengthMm); 

            view.Redraw();
        }

        public static void DebugDrawAxes(RhinoDoc doc, double[,] matrix, double scale, double length = 40)
        {
  
            Point3d origin = new Point3d(matrix[0, 3] * scale, matrix[1, 3] * scale, matrix[2, 3] * scale);
            Vector3d xAxis = new Vector3d(matrix[0, 0], matrix[1, 0], matrix[2, 0]);       
            Vector3d yAxis = new Vector3d(matrix[0, 1], matrix[1, 1], matrix[2, 1]);       
            Vector3d zAxis = -new Vector3d(matrix[0, 2], matrix[1, 2], matrix[2, 2]);      
            // Inner function to create or find a layer and add the line
            void AddAxis(string layerName, Color color, Vector3d direction)
            {
                int layerIndex = doc.Layers.FindByFullPath(layerName, -1);
                if (layerIndex == -1)
                {
                    Layer newLayer = new Layer { Name = layerName, Color = color };
                    layerIndex = doc.Layers.Add(newLayer);
                }

                Line line = new Line(origin, origin + direction * length * scale);
                ObjectAttributes attr = new ObjectAttributes { LayerIndex = layerIndex };
                doc.Objects.AddLine(line, attr);
            }

            AddAxis("CameraX_Axis", Color.Red, xAxis);
            AddAxis("CameraY_Axis", Color.Green, yAxis);
            AddAxis("CameraZ_Axis", Color.Blue, zAxis);

            doc.Views.Redraw();
        }

        private static fSpyProject ImportFSpyProject(string filePath)
        {
            using (FileStream fs = File.OpenRead(filePath))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                uint fileId = reader.ReadUInt32();
                if (fileId != 2037412710)
                {
                    throw new Exception("File loaded is not an fSpy project, file ID incorrect");
                }

                var projectVersion = reader.ReadUInt32();

                if (projectVersion != 1)
                {
                    throw new Exception($"Unsupported fSpy project file version {projectVersion}");
                }

                var stateStringSize = reader.ReadUInt32();
                var imageBufferSize = reader.ReadUInt32();

                byte[] stateBytes = reader.ReadBytes((int)stateStringSize);
                string jsonString = Encoding.UTF8.GetString(stateBytes);
                var state = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonString);
                var cameraParameters = new CameraParameters(state["cameraParameters"].ToString());
                var calibrationSettings = state["calibrationSettingsBase"] as JObject;

                var refDistanceUnit = calibrationSettings["referenceDistanceUnit"]?.ToString();
                var imageData = reader.ReadBytes((int)imageBufferSize);
                var tempImagePath = SaveTempImage(imageData);
                return new fSpyProject(cameraParameters, tempImagePath, refDistanceUnit);
            }
        }

        private static string SaveTempImage(byte[] imageData)
        {
            var tempFileName = Path.GetTempFileName();
            tempFileName = Path.ChangeExtension(tempFileName, ".png");

            using (var ms = new MemoryStream(imageData))
            using (var image = new Bitmap(ms))
            {
                image.Save(tempFileName, ImageFormat.Png);
            }

            return tempFileName;
        }

    }
}

