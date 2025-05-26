using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fSpyFileImport
{
    internal class fSpyProject
    {
        public CameraParameters CameraParameters { get; set; }
        public string ImageFilePath { get; set; }

        public string RefDistanceUnit { get; set; }

        public fSpyProject(CameraParameters cameraParameters, string imageFilePath, string refDistanceUnit)
        {
            CameraParameters = cameraParameters;
            ImageFilePath = imageFilePath;
            RefDistanceUnit = refDistanceUnit;
        }

    }
    internal class CameraParameters
    {
        public double[,] CameraMatrix{ get; set; }
        public double RelativeFocalLength { get; set; }
        public CameraParameters(string json)
        {
            JObject obj = JObject.Parse(json);
            CameraMatrix = ParseMatrix(obj);
            RelativeFocalLength = obj["relativeFocalLength"].Value<double>();

        }

        private static double[,] ParseMatrix(JObject obj)
        {
            
            JArray rows = (JArray)obj["cameraTransform"]["rows"];

            double[,] matrix = new double[4, 4];

            for (int i = 0; i < 4; i++)
            {
                JArray row = (JArray)rows[i];
                for (int j = 0; j < 4; j++)
                {
                    matrix[i, j] = row[j].Value<double>();
                }
            }

            return matrix;
        }
    }
}
