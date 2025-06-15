using System;

namespace ADS_B_Display_NET.Map.MapSrc
{
    /// <summary>
    /// Abstract class for earth view (C++에서 포팅).
    /// </summary>
    public abstract class EarthView
    {
        // 내비게이션 플래그 (C++ define을 C# const로 변환)
        public const int NAV_DRAG_PAN = 0x01;
        public const int NAV_DRAG_ZOOM = 0x02;

        public const int NAV_PAN_LEFT = 0x01;
        public const int NAV_PAN_RIGHT = 0x02;
        public const int NAV_PAN_UP = 0x04;
        public const int NAV_PAN_DOWN = 0x08;

        public const int NAV_ZOOM_IN = 0x10;
        public const int NAV_ZOOM_OUT = 0x20;

        protected MasterLayer _masterLayer;
        protected int _viewportWidth;
        protected int _viewportHeight;

        /// <summary>
        /// 현재 뷰어의 시점 정보
        /// </summary>
        public Eye Eye { get; protected set; }

        public EarthView(MasterLayer masterLayer)
        {
            _masterLayer = masterLayer;
            _viewportWidth = 0;
            _viewportHeight = 0;
            Eye = new Eye();
        }

        // 소멸자(Dispose 패턴 등 필요시 구현)
        //~EarthView() { }

        public abstract void Render(bool drawMap);
        public abstract void Animate();

        /// <summary>
        /// 뷰포트 크기 변경 시 호출
        /// </summary>
        public virtual void Resize(int width, int height)
        {
            _viewportWidth = width;
            _viewportHeight = height;
        }

        /// <summary>
        /// 마우스 드래그 시작
        /// </summary>
        public virtual int StartDrag(int x, int y, int flags)
        {
            return 0;
        }

        /// <summary>
        /// 마우스 드래그 중
        /// </summary>
        public virtual int Drag(int fromX, int fromY, int x, int y, int flags)
        {
            return 0;
        }

        /// <summary>
        /// 마우스 클릭
        /// </summary>
        public virtual int Click(int x, int y, int flags)
        {
            return 0;
        }

        /// <summary>
        /// 키보드 이동 시작
        /// </summary>
        public virtual int StartMovement(int flags)
        {
            return 0;
        }

        /// <summary>
        /// 키보드 이동 중지
        /// </summary>
        public virtual int StopMovement(int flags)
        {
            return 0;
        }

        /// <summary>
        /// 단일 이동 (키보드 입력)
        /// </summary>
        public virtual int SingleMovement(int flags)
        {
            return 0;
        }
    }
}