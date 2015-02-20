using Jiranator;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace Jirate
{
    public enum SortByEnum
    {
        Assigneee,
        Status,
        Id
    }
    public enum FilterEnum
    {
        All,
        Unresolved,
        Untested,
        CodeComplete,
        Last
    }

    public class JiraSprintViewModel : INotifyPropertyChanged
    {
        public async void Load()
        {
            var sprintKey = new SprintKey(RequestedSprint);
            var dataUrl = JiraAccess.GetSprintUri(sprintKey.Project, sprintKey.Sprint);

            try
            {
                _jsonString = null;
                await _semaphore.WaitAsync(TimeSpan.FromSeconds(60));
                BeginHttpGet(dataUrl);
                    
            }
            catch (Exception exc)
            {
                var str = exc.Message;
            }

            try
            {
                await _semaphore.WaitAsync(TimeSpan.FromSeconds(60));
                LoadJson(sprintKey, _jsonString);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        internal void GotoIssue(string v)
        {
            throw new NotImplementedException();
        }

        internal async void FindIssue(string queryText)
        {
            var dataUrl = JiraAccess.FindIssuesUri(queryText);

            try
            {
                _jsonString = null;
                await _semaphore.WaitAsync(TimeSpan.FromSeconds(60));
                BeginHttpGet(dataUrl);

            }
            catch (Exception exc)
            {
                var str = exc.Message;
            }

            try
            {
                await _semaphore.WaitAsync(TimeSpan.FromSeconds(60));
                LoadJson(null, _jsonString);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        SortByEnum _sortBy = SortByEnum.Id;

        internal void SortBy(SortByEnum sortBy)
        {
            _sortBy = sortBy;
            LoadJson(new SprintKey(RequestedSprint), _jsonString);
        }

        FilterEnum _filter = FilterEnum.All;
        internal void ToggleFilter()
        {
            _filter = (FilterEnum)(_filter + 1);
            if (_filter == FilterEnum.Last)
                _filter = FilterEnum.All;

                LoadJson(new SprintKey(RequestedSprint), _jsonString);
        }


        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        void BeginHttpGet(string url)
        {
            var request = WebRequest.Create(url) as HttpWebRequest;
            //request.ContentType = "application/json";
            request.Method = "GET";
            request.Headers["Authorization"] = "Basic " + EncodedCredentials;
            request.BeginGetResponse(GetResponseCallback, request);
        }

        string _jsonString;
        private void GetResponseCallback(IAsyncResult ar)
        {
            HttpWebRequest request = (HttpWebRequest)ar.AsyncState;
            HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(ar);
            Stream streamResponse = response.GetResponseStream();
            StreamReader streamRead = new StreamReader(streamResponse);
            _jsonString = streamRead.ReadToEnd();
            //LoadJson(new SprintKey("Test"), responseString);
            _semaphore.Release();
        }

        JiraSprint _sprint;
        public JiraSprint Sprint
        {
            get
            {
                return _sprint;
            }
            set
            {
                _sprint = value;
                OnPropertyChanged("Sprint");
            }
        }
        ObservableCollection<JiraIssueViewModel> _observableIssues;
        public ObservableCollection<JiraIssueViewModel> ObservableIssues
        {
            get
            {
                return _observableIssues;
            }
            set
            {
                _observableIssues = value;
                OnPropertyChanged("ObservableIssues");
            }
        }

        internal async void LoadSample()
        {
            Uri dataUri = new Uri("ms-appx:///Assets/SampleData.json");

            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(dataUri);
            string jsonText = await FileIO.ReadTextAsync(file);
            LoadJson(new SprintKey("Sample"), jsonText);
        }

        internal void LoadJson(SprintKey sprintKey, string jsonText)
        { 
            Sprint = JiraSprint.Parse(sprintKey, JObject.Parse(jsonText));
            var rv = new ObservableCollection<JiraIssueViewModel>();

            IEnumerable<JiraIssue> issues = Sprint.Issues;
            if (_filter == FilterEnum.Unresolved)
                issues = Sprint.Issues.Where(i => !i.IsResolved);
            else if (_filter == FilterEnum.Untested)
                issues = Sprint.Issues.Where(i => !(i.IsTesting || i.IsResolved));
            else if (_filter == FilterEnum.CodeComplete)
                issues = Sprint.Issues.Where(i => (i.IsTesting || i.IsResolved));
            else
                issues = Sprint.Issues;

            IOrderedEnumerable<JiraIssue> sortedIssues;
            if (_sortBy == SortByEnum.Assigneee)
                sortedIssues = issues.OrderBy(i => i.Assignee);
            else if (_sortBy == SortByEnum.Status)
                sortedIssues = issues.OrderBy(i => i.CalcedStatus);
            else
                sortedIssues = issues.OrderBy(i => i.Key);


            foreach (var issue in sortedIssues)
            {
                rv.Add(new JiraIssueViewModel(issue));
                /*
                var lvi = new ListViewItem();
                lvi.Tag = issue;
                lvi.Content = issue.Key + " " + issue.Summary + " " + issue.CalcedStatus + " " + issue.Assignee;
                lvi.FontSize = 20;
                ObservableIssues.Add(lvi);
                */
            }
            ObservableIssues = rv;
        }

        string _requestedSprint = "MOB Sprint 42";
        public string RequestedSprint
        {
            get
            {
                return _requestedSprint;
            }
            set
            {
                _requestedSprint = value;
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string info)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(info));
            }
        }

        static string EncodedCredentials
        {
            get
            {
                return JiraAccess.GetEncodedCredentials("orashkevych", "roadnet");
            }
        }
    }
}
