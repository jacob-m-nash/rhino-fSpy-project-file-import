using Eto.Drawing;
using Newtonsoft.Json;
using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using Rhino.UI;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;


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
        public override string EnglishName => "fSpyFileImportCommand";

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

            if (!System.IO.File.Exists(filePath))
            {
                RhinoApp.WriteLine("File not found.");
                return Result.Failure;
            }

            try
            {
                var project = ImportFSpyProject(filePath);
                //TODO add project to rhino 
            }
            catch (Exception e)
            {
                RhinoApp.WriteLine(e.Message);
                return Result.Failure;
            }



            RhinoApp.WriteLine("Done");
            return Result.Success;
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

