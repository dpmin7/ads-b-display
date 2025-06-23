using ADS_B_Display.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ADS_B_Display.Views
{
    internal class AircraftListViewModel : NotifyPropertyChangedBase
    {
        public ICommand CMD_VisibleChanged { get; }

        private IDisposable _eventVisibleAircraftListUpdated;


        private System.Collections.IEnumerable visibleAircraftList;
        public System.Collections.IEnumerable VisibleAircraftList { get => visibleAircraftList; set => SetProperty(ref visibleAircraftList, value); }


        public AircraftListViewModel()
        {
            CMD_VisibleChanged = new DelegateCommand(OnVisibleChanged);
            VisibleAircraftList = new List<Aircraft>();
        }

        private void OnVisibleChanged(object obj)
        {
            if (obj is bool isVisible)
            {
                if (isVisible)
                {
                    _eventVisibleAircraftListUpdated = EventBus.Observe(EventIds.EvtAircraftListViewUpdated).ObserveOnDispatcher()
                                                               .Subscribe(e => UpdateList(e));
                }
                else
                {
                    _eventVisibleAircraftListUpdated.Dispose();
                }
            }
            else
            {
                throw new ArgumentException("Expected a boolean value for visibility state.", nameof(obj));
            }
        }

        private void UpdateList(object e)
        {
            
        }
    }
}
