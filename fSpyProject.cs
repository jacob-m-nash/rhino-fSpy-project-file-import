using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fSpyFileImport
{
    internal class fSpyProject
    {
        CameraParameters CameraParameters { get; set; }
        private string ImageFilePath { get; set; }

        private string RefDistanceUnit { get; set; }

        public fSpyProject(CameraParameters cameraParameters, string imageFilePath, string refDistanceUnit)
        {
            CameraParameters = cameraParameters;
            ImageFilePath = imageFilePath;
            this.RefDistanceUnit = refDistanceUnit;
        }

    }
    internal class CameraParameters
    {
        public CameraParameters(string json)
        {
            
        }
    }
}
