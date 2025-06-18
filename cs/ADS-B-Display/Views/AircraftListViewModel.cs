using ADS_B_Display.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ADS_B_Display.Views
{
    internal class AircraftListViewModel : NotifyPropertyChangedBase
    {
        public ICommand CMD_VisibleChanged { get; }


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
                    // Logic to execute when the view becomes visible
                    EventBus.Publish("AircraftListViewVisible", true);
                }
                else
                {
                    // Logic to execute when the view becomes invisible
                    EventBus.Publish("AircraftListViewVisible", false);
                }
            }
            else
            {
                throw new ArgumentException("Expected a boolean value for visibility state.", nameof(obj));
            }
        }

        
    }
}
