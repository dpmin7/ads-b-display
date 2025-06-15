using ADS_B_Display;
using ADS_B_Display.Map.MapSrc;
using Microsoft.SqlServer.Server;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Numerics;
using System.Windows;

namespace ADS_B_Display_NET.Map.MapSrc
{
    /// <summary>
    /// Flat representation of Earth.
    /// Simplest possible earth representation. Orthogonal projection, pitch cannot be changed and north is always 'up'.
    /// </summary>
    public class FlatEarthView : EarthView
    {
        private int _currentMovementFlags;
        private Eye _savedZoomEye;
        private Eye _savedPanEye;

        public FlatEarthView(MasterLayer masterLayer) : base(masterLayer)
        {
            _currentMovementFlags = 0;
        }

        public override void Render(bool drawMap)
        {
            // x and y span of viewable size in global coords
            double aspect = (double)_viewportWidth / (double)_viewportHeight;
            double yspan = Eye.YSpan(aspect);
            double xspan = Eye.XSpan(aspect);

            // setup projection (OpenGL 관련 부분은 실제 구현에 맞게 대체 필요)
            GL.MatrixMode(MatrixMode.Projection);
            GlUtil.Projection2D(0, 0, _viewportWidth, _viewportHeight);
            
            // calculate virtual coordinates for sides of world rectangle
            double worldLeftVirtual = ((-0.5 - Eye.X) * _viewportWidth / xspan) + _viewportWidth / 2.0;
            double worldRightVirtual = ((0.5 - Eye.X) * _viewportWidth / xspan) + _viewportWidth / 2.0;
            double worldTopVirtual = ((0.5 - Eye.Y) * _viewportHeight / yspan) + _viewportHeight / 2.0;
            double worldBottomVirtual = ((-0.5 - Eye.Y) * _viewportHeight / yspan) + _viewportHeight / 2.0;

            // Region 생성
            Region rgn = new Region(
                new Vector3d(0, 0, 0),
                new Vector3d((float)_viewportWidth, 0, 0),
                new Vector3d((float)_viewportWidth, (float)_viewportHeight, 0),
                new Vector3d(0, (float)_viewportHeight, 0),
                new Vector2d((float)(Eye.X - xspan / 2.0), (float)(Eye.Y - yspan / 2.0)),
                new Vector2d((float)(Eye.X + xspan / 2.0), (float)(Eye.Y + yspan / 2.0)),
                new Vector3d(0, 0, 0),
                new Vector3d((float)_viewportWidth, 0, 0),
                new Vector3d((float)_viewportWidth, (float)_viewportHeight, 0),
                new Vector3d(0, (float)_viewportHeight, 0)
            );

            // tune coords for the cases where earth bounds appear on screen
            if (worldLeftVirtual > 0.0) {
                rgn.V[0].X = rgn.V[3].X = (float)worldLeftVirtual;
                rgn.P[0].X = rgn.P[3].X = (float)worldLeftVirtual;
                rgn.W[0].X = -0.5f;
            }
            if (worldRightVirtual < _viewportWidth) {
                rgn.V[1].X = rgn.V[2].X = (float)worldRightVirtual;
                rgn.P[1].X = rgn.P[2].X = (float)worldRightVirtual;
                rgn.W[1].X = 0.5f;
            }
            if (worldBottomVirtual > 0.0) {
                rgn.V[0].Y = rgn.V[1].Y = (float)worldBottomVirtual;
                rgn.P[0].Y = rgn.P[1].Y = (float)worldBottomVirtual;
                rgn.W[0].Y = -0.5f;
            }
            if (worldTopVirtual < _viewportHeight) {
                rgn.V[2].Y = rgn.V[3].Y = (float)worldTopVirtual;
                rgn.P[2].Y = rgn.P[3].Y = (float)worldTopVirtual;
                rgn.W[1].Y = 0.5f;
            }

            // (Form1->Map_v 등은 WPF에서는 필요시 ViewModel 등으로 전달)
            // 예시: MapViewModel.UpdateMapRegion(rgn);
            ((MainWindow)Application.Current.MainWindow).Map_v[0] = rgn.V[0];
            ((MainWindow)Application.Current.MainWindow).Map_v[1] = rgn.V[1];
            ((MainWindow)Application.Current.MainWindow).Map_v[2] = rgn.V[2];
            ((MainWindow)Application.Current.MainWindow).Map_v[3] = rgn.V[3];
            ((MainWindow)Application.Current.MainWindow).Map_w[0] = rgn.W[0];
            ((MainWindow)Application.Current.MainWindow).Map_w[1] = rgn.W[1];
            ((MainWindow)Application.Current.MainWindow).Map_p[0] = rgn.P[0];
            ((MainWindow)Application.Current.MainWindow).Map_p[1] = rgn.P[1];
            ((MainWindow)Application.Current.MainWindow).Map_p[2] = rgn.P[2];
            ((MainWindow)Application.Current.MainWindow).Map_p[3] = rgn.P[3];
            
            // call master layer
            if (drawMap)
                _masterLayer.RenderRegion(rgn);
        }

        public override void Animate()
        {
            NormalizeEye();
        }

        public override int StartDrag(int x, int y, int flags)
        {
            if ((flags & NAV_DRAG_PAN) != 0)
                _savedPanEye = new Eye(Eye.X, Eye.Y, Eye.H, Eye.Orient, Eye.Pitch, Eye.Fov);
            if ((flags & NAV_DRAG_ZOOM) != 0)
                _savedZoomEye = new Eye(Eye.X, Eye.Y, Eye.H, Eye.Orient, Eye.Pitch, Eye.Fov);
            return 1;
        }

        public override int Drag(int fromX, int fromY, int x, int y, int flags)
        {
            double aspect = (double)_viewportWidth / (double)_viewportHeight;
            double yspan = Eye.YSpan(aspect);
            double xspan = Eye.XSpan(aspect);

            if ((flags & NAV_DRAG_PAN) != 0) {
                Eye.Y = _savedPanEye.Y + (double)(y - fromY) / _viewportHeight * yspan;
                Eye.X = _savedPanEye.X - (double)(x - fromX) / _viewportWidth * xspan;
            }
            if ((flags & NAV_DRAG_ZOOM) != 0) {
                if (y - fromY < 0)
                    Eye.H = _savedZoomEye.H * (1.0 + (double)(y - fromY) / _viewportHeight);
                else
                    Eye.H = _savedZoomEye.H / (1.0 - (double)(y - fromY) / _viewportHeight);
            }
            return 1;
        }

        public override int StartMovement(int flags)
        {
            _currentMovementFlags |= flags;
            return 1;
        }

        public override int StopMovement(int flags)
        {
            _currentMovementFlags &= ~flags;
            return 1;
        }

        public override int SingleMovement(int flags)
        {
            if ((flags & NAV_ZOOM_IN) != 0)
                Eye.H /= 1.3;
            if ((flags & NAV_ZOOM_OUT) != 0)
                Eye.H *= 1.3;
            NormalizeEye();
            return 1;
        }

        const double MIN_HEIGHT = 10.0 / 40000000.0;
        const double MAX_HEIGHT = 1.0;
        /// <summary>
        /// Fix eye coordinates after movements
        /// </summary>
        private void NormalizeEye()
        {
            if (Eye.X < -0.5) Eye.X = -0.5;
            if (Eye.X > 0.5) Eye.X = 0.5;
            if (Eye.Y < -0.5) Eye.Y = -0.5;
            if (Eye.Y > 0.5) Eye.Y = 0.5;
            
            if (Eye.H < MIN_HEIGHT) Eye.H = MIN_HEIGHT;
            if (Eye.H > MAX_HEIGHT) Eye.H = MAX_HEIGHT;
        }
    }
}