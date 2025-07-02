using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace ADS_B_Display.Views.Popup
{
    internal class TypeFilterSettingPopupVM : NotifyPropertyChangedBase
    {
        private Action<List<string>> CheckedListCB = null;

        public ObservableCollection<TypeItem> AircraftTypeList { get; set; } = new ObservableCollection<TypeItem>();
        private ObservableCollection<TypeItem> _selectedAircraftTypeList = new ObservableCollection<TypeItem>();
        public ObservableCollection<TypeItem> SelectedAircraftTypeList
        {
            get => _selectedAircraftTypeList;
            set
            {
                _selectedAircraftTypeList = value;
                OnPropertyChanged(nameof(SelectedAircraftTypeList));
            }
        }

        public TypeFilterSettingPopupVM(IList<string> aircraftTypeList, IList<string> selectedAircraftTypeList, Action<List<string>> checkedListCB)
        {
            //AircraftTypeList = new ObservableCollection<TypeItem>(aircraftTypeList.Select(x => new TypeItem(x, false, ChangeData)));
            foreach (string item in aircraftTypeList)
            {
                if (selectedAircraftTypeList.Contains(item))
                {
                    var temp = new TypeItem(item, true, ChangeData);
                    AircraftTypeList.Add(temp);
                    SelectedAircraftTypeList.Add(temp);
                }
                else
                {
                    AircraftTypeList.Add(new TypeItem(item, false, ChangeData));
                }
            }

            CheckedListCB = checkedListCB;
            Cmd_Apply = new DelegateCommand(Apply);
        }

        private void ChangeData(TypeItem item)
        {
            if (item.IsChecked)
            {
                SelectedAircraftTypeList.Add(item);
            }
            else
            {
                SelectedAircraftTypeList.Remove(item);
            }
        }

        private void Apply(object obj)
        {
            CheckedListCB?.Invoke(AircraftTypeList.Where(x => x.IsChecked).Select(x => x.Type).ToList());
        }

        public ICommand Cmd_Apply { get; }
    }

    public class TypeItem : NotifyPropertyChangedBase
    {
        private Action<TypeItem> SendMyself;
        public string Type { get; set; }
        private bool isChecked;
        public bool IsChecked {
            get => isChecked;
            set
            {
                isChecked = value;
                OnPropertyChanged(nameof(IsChecked));
                SendMyself?.Invoke(this);
            }
        }
        public TypeItem(string type, bool isChecked, Action<TypeItem> sendMyself)
        {
            Type = type;
            IsChecked = isChecked;
            SendMyself = sendMyself;
        }
    }
}
