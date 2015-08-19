using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Toggl.Joey.UI.Fragments;
using Toggl.Phoebe.Net;
using XPlatUtils;

namespace Toggl.Joey.UI.Activities
{
    [Activity (Label = "ClientListActivity",
               ScreenOrientation = ScreenOrientation.Portrait,
               Theme = "@style/Theme.Toggl.App")]
    public class ClientListActivity : BaseActivity
    {
        public static readonly string ExtraWorkspaceId = "com.toggl.timer.client_list_workspace_id";

        protected override void OnCreateActivity (Bundle state)
        {
            base.OnCreateActivity (state);
            SetContentView (Resource.Layout.ClientListActivity);

            var user = ServiceContainer.Resolve<AuthManager> ().User;

            SupportFragmentManager.BeginTransaction ()
            .Add (Resource.Id.ClientListActivityLayout, new ClientListFragment (user.DefaultWorkspaceId))
            .Commit ();
        }

        public static List<Guid> GetIntentWorkspaceId (Android.Content.Intent intent)
        {
            var extras = intent.Extras;
            if (extras == null) {
                var list = new List<Guid> ();
                list.Add (Guid.Empty);
                return list;
            }

            var extraGuidArray = extras.GetStringArray (ExtraWorkspaceId);
            return GetGuidList (extraGuidArray);
        }

        public static List<Guid> GetGuidList (string[] ids)
        {
            var list = new List<Guid> ();
            foreach (var stringGuid in ids) {
                var guid = new Guid (stringGuid);
                list.Add (guid);
            }
            return list;
        }
    }
}
