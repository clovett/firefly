using Microsoft.Storage;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BleLights.SharedControls
{
    public class Settings : INotifyPropertyChanged
    {
        static Mutex fileLock = new Mutex();
        int playSpeed = 3;
        string armColor = "red";
        int burnTime = 500; // half a second

        static Settings _instance;

        public Settings()
        {
            _instance = this;
        }

        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Settings();
                }
                return _instance;
            }
        }

        public int PlaySpeed
        {
            get
            {
                return playSpeed;
            }

            set
            {
                if (playSpeed != value)
                {
                    playSpeed = value;
                    OnPropertyChanged("HasMotionSensor");
                }
            }
        }

        public int BurnTime
        {
            get
            {
                return burnTime;
            }

            set
            {
                if (burnTime != value)
                {
                    burnTime = value;
                    OnPropertyChanged("BurnTime");
                }
            }
        }

        public string ArmColor
        {
            get
            {
                return armColor;
            }

            set
            {
                if (armColor != value)
                {
                    armColor = value;
                    OnPropertyChanged("ArmColor");
                }
            }
        }


        public static async Task<Settings> LoadAsync()
        {
            Settings result = null;
            try
            {
                fileLock.WaitOne();
                var store = new IsolatedStorage<Settings>();
                result = await store.LoadFromFileAsync(Windows.Storage.ApplicationData.Current.LocalFolder, "settings.xml");
                if (result == null)
                {
                    result = new Settings();
                    await result.SaveAsync();
                }
            }
            finally
            {
                fileLock.ReleaseMutex();
            }
            return result;
        }

        public async Task SaveAsync()
        {
            try
            {
                fileLock.WaitOne();
                var store = new IsolatedStorage<Settings>();
                await store.SaveToFileAsync(Windows.Storage.ApplicationData.Current.LocalFolder, "settings.xml", this);
            }
            finally
            {
                fileLock.ReleaseMutex();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

    }

}
