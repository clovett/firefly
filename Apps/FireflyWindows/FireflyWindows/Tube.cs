using System.ComponentModel;

namespace FireflyWindows
{
    class Tube : INotifyPropertyChanged
    {
        string name;
        bool fired;
        bool firing;
        bool failed;
        int number;

        public string Name
        {
            get { return name; }
            set { name = value; OnPropertyChanged("Name"); }
        }

        public bool Fired
        {
            get { return fired; }
            set { fired = value; OnPropertyChanged("Fired"); }
        }

        public bool Firing
        {
            get { return firing; }
            set { firing = value; OnPropertyChanged("Firing"); }
        }

        public bool Failed
        {
            get { return failed; }
            set { failed = value; OnPropertyChanged("Failed"); }
        }

        public int Number
        {
            get { return number; }
            set { number = value; OnPropertyChanged("Number"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                UiDispatcher.RunOnUIThread(() =>
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(name));
                });

            }
        }
    }
}
