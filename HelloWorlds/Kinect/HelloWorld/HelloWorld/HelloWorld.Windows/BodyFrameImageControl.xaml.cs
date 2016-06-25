using System.Linq;

namespace HelloWorld
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Windows.Foundation;
    using Windows.UI;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Shapes;
    using WindowsPreview.Kinect;

    public partial class BodyFrameImageControl : UserControl
    {
        #region CONSTANTS
        private static readonly float ENGAGEMENT_RADIUS = 0.75f;
        private static readonly int HEAD_WIDTH = 50;
        private static readonly int REGULAR_WIDTH = 20;
        private static readonly Brush[] BodyBrushes =
        {
            new SolidColorBrush(Colors.Green),
            new SolidColorBrush(Colors.Blue),
            new SolidColorBrush(Colors.Orange),
            new SolidColorBrush(Colors.Pink),
            new SolidColorBrush(Colors.Aqua),
            new SolidColorBrush(Colors.Yellow)
        };
        private static readonly Brush InferredBrush = new SolidColorBrush(Colors.Gray);
        private static readonly Brush EngagementBrush = new SolidColorBrush(Colors.Red);
        private static readonly JointType[] renderedJointTypes = { JointType.Head, JointType.HandLeft, JointType.HandRight, JointType.SpineMid };  //TODO remove filter for joints
        #endregion
        #region FIELDS
        private FrameDescription colorFrameDescription;
        private CoordinateMapper coordinateMapper;
        private readonly List<Vector4> hotZones = new List<Vector4>();
        #endregion
        public BodyFrameImageControl()
        {
            InitializeComponent();

            this.hotZones.Add(new Vector4() { W = 1.0f, X = 1.141569f, Y = -0.2308079f, Z = 2.408344f });  //Todo add more hotzones if you like

            //Positions of head and corner of desk (for reference)
            //ENGAGEMENT_RADIUS = DistanceBetween2Vectors(
            //    new Vector4() { W = 1.0f, X = 1.141569f, Y = -0.2308079f, Z = 2.408344f }, 
            //    new Vector4() { W = 1.0f, X = 0.5592872f, Y = 0.5260094f, Z = 2.422873f});
        }
        #region PROPERTIES
        public static object Brushes { get; private set; }
        #endregion
        #region METHODS
        public void Initialise(FrameDescription colorFrameDescription,
          CoordinateMapper coordinateMapper)
        {
            this.colorFrameDescription = colorFrameDescription;
            this.coordinateMapper = coordinateMapper;
        }
        public void DrawBodies(Body[] bodies)
        {
            // We take the naive approach of getting rid of everything for now in the
            // hope of simplicity. We could do something better. 
            this.canvas.Children.Clear();

            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].IsTracked)
                {
                    this.DrawBody(bodies[i], BodyBrushes[i]);
                }
            }
        }
        private void DrawBody(Body body, Brush brush)
        {
            Debug.WriteLine("Body ID: {0} : {1}", body.ToString(), body.TrackingId);

            foreach (var entry in body.Joints)
            {
                JointType jointType = entry.Key;
                Joint joint = entry.Value;

                foreach (JointType renderedJointType in renderedJointTypes)
                {
                    if (jointType == renderedJointType)
                    {
                        if (joint.TrackingState != TrackingState.NotTracked)
                        {
                            Rectangle rect = null;
                            Point position2d = this.MapPointToCanvasSpace(joint.Position);

                            if (jointType == JointType.Head)
                            {
                                Debug.WriteLine("HEAD LOC:{0},{1},{2}", joint.Position.X, joint.Position.Y, joint.Position.Z);

                                Vector4 headpos = new Vector4()
                                {
                                    W = 1.0f,
                                    X = joint.Position.X,
                                    Y = joint.Position.Y,
                                    Z = joint.Position.Z
                                };

                                List<float> hotZoneDistances = new List<float>();
                                foreach (var hotZone in hotZones)
                                {
                                    float distance = DistanceBetween2Vectors(headpos, hotZone);
                                    hotZoneDistances.Add(distance);
                                }

                                float headToZone = hotZoneDistances.Min();
                                bool inHotZone = (headToZone < ENGAGEMENT_RADIUS);
                                Debug.WriteLine("Head DISTANCE {0}_IN HOT ZONE?_{1}", headToZone, inHotZone);

                                rect =
                                    this.MakeRectangleForJoint(jointType, joint.TrackingState, inHotZone, brush,
                                        position2d);
                            }

                            #region Draw the canvas
                            
                            if (!double.IsInfinity(position2d.X) && !double.IsInfinity(position2d.Y))
                            {
                                
                                Ellipse ellipse =
                                  this.MakeEllipseForJoint(jointType, joint.TrackingState, brush, position2d);

                                if (rect != null)
                                {
                                    this.canvas.Children.Add(rect);
                                }

                                this.canvas.Children.Add(ellipse);
                            }
                            #endregion
                        }
                    }
                }
            }
        }
        private Ellipse MakeEllipseForJoint(
          JointType jointType,
          TrackingState trackingState,
          Brush brush,
          Point position2d)
        {
            int width = jointType == JointType.Head ? HEAD_WIDTH : REGULAR_WIDTH;

            Ellipse ellipse = new Ellipse()
            {
                Width = width,
                Height = width,
                Fill = trackingState == TrackingState.Inferred ? InferredBrush : brush
            };
            Canvas.SetLeft(ellipse, position2d.X - (width / 2));
            Canvas.SetTop(ellipse, position2d.Y - (width / 2));
            return (ellipse);
        }
        private Rectangle MakeRectangleForJoint(
          JointType jointType,
          TrackingState trackingState,
          bool inHotZone,
          Brush brush,
          Point position2d)
        {
            int width = jointType == JointType.Head ? HEAD_WIDTH : REGULAR_WIDTH;

            Rectangle rectangle = new Rectangle()
            {
                Width = width,
                Height = width,
                Fill = trackingState == TrackingState.Inferred ? InferredBrush : brush
            };

            if (inHotZone)
            {
                rectangle.Fill = EngagementBrush;
            }

            Canvas.SetLeft(rectangle, position2d.X - (width / 2));
            Canvas.SetTop(rectangle, position2d.Y - (width / 2));

            return (rectangle);
        }
        Point MapPointToCanvasSpace(CameraSpacePoint point)
        {
            ColorSpacePoint colorSpacePoint =
              this.coordinateMapper.MapCameraPointToColorSpace(point);

            Point mappedPoint = new Point(
              colorSpacePoint.X / this.colorFrameDescription.Width * this.canvas.ActualWidth,
              colorSpacePoint.Y / this.colorFrameDescription.Height * this.canvas.ActualHeight);

            return (mappedPoint);
        }
        private static float DistanceBetween2Vectors(Vector4 firstVector4, Vector4 secondVector4)
        {
            Vector4 deltaVector4 = new Vector4()
            {
                X = Math.Abs(secondVector4.X - firstVector4.X),
                Y = Math.Abs(secondVector4.Y - secondVector4.Y),
                Z = Math.Abs(secondVector4.Z - firstVector4.Z)
            };

            return
                (float)Math.Sqrt(
                    deltaVector4.X * deltaVector4.X +
                    deltaVector4.Y * deltaVector4.Y +
                    deltaVector4.Z + deltaVector4.Z);
        }
        #endregion
    }
}