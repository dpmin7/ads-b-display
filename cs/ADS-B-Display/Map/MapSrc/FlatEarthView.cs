﻿using ADS_B_Display;
using ADS_B_Display.Map.MapSrc;
using Microsoft.SqlServer.Server;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Windows;

namespace ADS_B_Display.Map.MapSrc
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

        private double aspect;
        public double Yspan { get; private set; }
        public double Xspan { get; private set; }
        private Region rgn;
        public override Region PreRender()
        {
            // x and y span of viewable size in global coords
            aspect = (double)_viewportWidth / _viewportHeight;
            Yspan = Eye.YSpan(aspect);
            Xspan = Eye.XSpan(aspect);

            // Region 생성
            rgn = new Region(
                new Vector3d(0, 0, 0),
                new Vector3d((float)_viewportWidth, 0, 0),
                new Vector3d((float)_viewportWidth, (float)_viewportHeight, 0),
                new Vector3d(0, (float)_viewportHeight, 0),
                new Vector2d((float)(Eye.X - Xspan / 2.0), (float)(Eye.Y - Yspan / 2.0)),
                new Vector2d((float)(Eye.X + Xspan / 2.0), (float)(Eye.Y + Yspan / 2.0)),
                new Vector3d(0, 0, 0),
                new Vector3d((float)_viewportWidth, 0, 0),
                new Vector3d((float)_viewportWidth, (float)_viewportHeight, 0),
                new Vector3d(0, (float)_viewportHeight, 0)
            );

            // calculate virtual coordinates for sides of world rectangle
            double worldLeftVirtual = ((-0.5 - Eye.X) * _viewportWidth / Xspan) + _viewportWidth / 2.0;
            double worldRightVirtual = ((0.5 - Eye.X) * _viewportWidth / Xspan) + _viewportWidth / 2.0;
            double worldTopVirtual = ((0.5 - Eye.Y) * _viewportHeight / Yspan) + _viewportHeight / 2.0;
            double worldBottomVirtual = ((-0.5 - Eye.Y) * _viewportHeight / Yspan) + _viewportHeight / 2.0;

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

            return rgn;
        }

        public override void Render(bool drawMap)
        {
            // setup projection
            GL.MatrixMode(MatrixMode.Projection);
            GlUtil.Projection2D(0, 0, _viewportWidth, _viewportHeight);

            // calculate virtual coordinates for sides of world rectangle
            // 이 값은 화면상에서 360도 월드의 폭이 몇 픽셀인지를 계산합니다.
            double worldScreenWidth = _viewportWidth / Xspan;
    
            // MainWindow의 Map_v, Map_w 업데이트 (기존과 동일)
    
            // --- 맵 3번 그리기 로직 ---
            if (drawMap)
            {
                // 1. 중앙 맵 그리기
                _masterLayer.RenderRegion(rgn);

                // 2. 오른쪽 맵 그리기
                GL.PushMatrix();
                GL.Translate(worldScreenWidth, 0, 0); // 월드의 너비만큼 오른쪽으로 이동
                _masterLayer.RenderRegion(rgn);
                GL.PopMatrix();

                // 3. 왼쪽 맵 그리기
                GL.PushMatrix();
                GL.Translate(-worldScreenWidth, 0, 0); // 월드의 너비만큼 왼쪽으로 이동
                _masterLayer.RenderRegion(rgn);
                GL.PopMatrix();
            }
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
            const double MaxLat = 85.0;
            const double MinLat = -85.0;

            double aspect = (double)_viewportWidth / (double)_viewportHeight;
            double yspan = Eye.YSpan(aspect);
            double xspan = Eye.XSpan(aspect);

            if ((flags & NAV_DRAG_PAN) != 0) {
                double halfSpan = yspan / 2.0;
                double newY = _savedPanEye.Y + (double)(y - fromY) / _viewportHeight * yspan;
                // 위도 제한: 화면 위/아래가 85도를 넘지 않도록 Eye.Y 제한
                double latTop = (newY + halfSpan) * 180.0;
                double latBottom = (newY - halfSpan) * 180.0;
                if (latTop > MaxLat) {
                    newY = MaxLat / 180.0 - halfSpan;
                } else if (latBottom < MinLat) {
                    newY = MinLat / 180.0 + halfSpan;
                }

                Eye.Y = newY;// _savedPanEye.Y + (double)(y - fromY) / _viewportHeight * yspan;
                Eye.X = _savedPanEye.X - (double)(x - fromX) / _viewportWidth * xspan;
                //Debug.WriteLine($"Pan X: {Eye.X}, Y: {Eye.Y}");
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

        const double MaxZoomOut = 0.4;  // 줌 아웃 최대값 (더 크면 더 멀리서 보는 것)
        public override int SingleMovement(int flags)
        {
            if ((flags & NAV_ZOOM_IN) != 0)
                Eye.H /= 1.3;

            if ((flags & NAV_ZOOM_OUT) != 0) {
                // 줌 아웃 최대값 초과하지 않도록 제한
                double newZoom = Eye.H * 1.3;
                Eye.H = Math.Min(newZoom, 0.769230769230769);
                //Debug.WriteLine($"H : {Eye.H}");
            }

            NormalizeEye();
            return 1;
        }

        const double MIN_HEIGHT = 10.0 / 400000.0;
        const double MAX_HEIGHT = 1.0;

        /// <summary>
        /// Fix eye coordinates after movements
        /// </summary>
        private void NormalizeEye()
        {
            // Eye.X (경도)가 1.0 (360도) 범위를 넘어가면 wrap-around 되도록 수정
            // (예: 0.6은 -0.4로, -0.6은 0.4로)
            if (Eye.X >= 0.5)
            {
                Eye.X -= 1.0;
            }
            if (Eye.X < -0.5)
            {
                Eye.X += 1.0;
            }
            if (Eye.Y < -0.5) Eye.Y = -0.5;
            if (Eye.Y > 0.5) Eye.Y = 0.5;

            if (Eye.H < MIN_HEIGHT) Eye.H = MIN_HEIGHT;
            if (Eye.H > MAX_HEIGHT) Eye.H = MAX_HEIGHT;
        }

        public override Region GetCurrentRegion()
        {
            double aspect = (double)_viewportWidth / (double)_viewportHeight;
            double yspan = Eye.YSpan(aspect);
            double xspan = Eye.XSpan(aspect);
            return new Region(
                new Vector3d(0, 0, 0),
                new Vector3d((float)_viewportWidth, 0, 0),
                new Vector3d((float)_viewportWidth, (float)_viewportHeight, 0),
                new Vector3d(0, (float)_viewportHeight, 0),
                new Vector2d((float)(Eye.X - xspan / 2.0), (float)(Eye.Y - yspan / 2.0)),
                new Vector2d((float)(Eye.X + xspan / 2.0), (float)(Eye.Y + yspan / 2.0))
            );
        }
    }
}