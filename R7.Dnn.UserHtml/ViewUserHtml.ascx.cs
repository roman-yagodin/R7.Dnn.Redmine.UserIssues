//
// ViewUserHtml.ascx.cs
//
// Author:
//       Roman M. Yagodin <roman.yagodin@gmail.com>
//
// Copyright (c) 2018 Roman M. Yagodin
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using DotNetNuke.Entities.Icons;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Modules.Actions;
using DotNetNuke.Entities.Users;
using DotNetNuke.Security;
using DotNetNuke.Services.Exceptions;
using R7.Dnn.Extensions.ControlExtensions;
using R7.Dnn.Extensions.Modules;
using R7.Dnn.Extensions.Utilities;
using R7.Dnn.UserHtml.Data;
using R7.Dnn.UserHtml.Models;
using R7.Dnn.UserHtml.ViewModels;

namespace R7.Dnn.UserHtml
{
    public class ViewUserHtml : PortalModuleBase<UserHtmlSettings>, IActionable
    {
        #region Controls

        protected Literal litUserHtml;
        protected TextBox txtSearchUser;
        protected DropDownList selUser;
        protected HyperLink lnkEditUserHtml;
        protected Panel pnlUser;
        protected Panel pnlSearchUser;
        protected Panel pnlSelectUser;
        protected Panel pnlUserHtml;
        protected Label lblSearchResult;
        protected LinkButton btnSearchUser;

        #endregion

        #region Session-state properties

        string _sessionSearchText;
        protected string SessionSearchText {
            get { return _sessionSearchText ?? (_sessionSearchText = (string) Session ["UserHtml_SearchText_" + TabModuleId]); }
            set { Session ["UserHtml_SearchText_" + TabModuleId] = _sessionSearchText = value; }
        }

        int? _sessionUserId;
        protected int? SessionUserId {
            get { return _sessionUserId ?? (_sessionUserId = (int?) Session ["UserHtml_UserId_" + TabModuleId]); }
            set { Session ["UserHtml_UserId_" + TabModuleId] = _sessionUserId = value; }
        }

        #endregion

        void TryRestoreState ()
        {
            if (SessionSearchText != null) {
                txtSearchUser.Text = SessionSearchText;
                btnSearchUser_Click_Internal (txtSearchUser.Text, false);

                if (SessionUserId != null) {
                    var selectedIndex = selUser.FindIndexByValue (SessionUserId.Value, -1);
                    if (selectedIndex >= 0) {
                        selUser.SelectedIndex = selectedIndex;
                        selUser_SelectedIndexChanged_Internal (SessionUserId);
                    }
                    else {
                        txtSearchUser.Text = string.Empty;
                        pnlSelectUser.Visible = false;
                    }
                }
            }
        }

        protected override void OnInit (EventArgs e)
        {
            base.OnInit (e);

            pnlUser.Visible = false;
            pnlSelectUser.Visible = false;

            txtSearchUser.Attributes.Add ("placeholder", LocalizeString ("txtSearchUser_Placeholder.Text"));
        }

        protected override void OnLoad (EventArgs e)
        {
            base.OnLoad (e);

            try {
                if (!IsPostBack) {
                    var showModule = false; 
                    // TODO: Add support for userid querystring parameter?
                    // TODO: Don't show content on profile page of different user.
                    if (Request.IsAuthenticated) {
                        if (!IsOnProfilePage () || IsOnOwnProfilePage ()) {
                            ShowMyContent ();
                            showModule = true;
                        }
                        if (IsEditable) {
                            ShowEditPanel ();
                            showModule = true;
                        }
                    }

                    if (!showModule) {
                        HideModule ();
                    }
                }
            }
            catch (Exception ex) {
                Exceptions.ProcessModuleLoadException (this, ex);
            }
        }

        int? GetUserProfileId () => TypeUtils.ParseToNullable<int> (Request.QueryString ["userid"]);

        // TODO: Use profile page from portal settings?
        bool IsOnProfilePage () => GetUserProfileId () != null;

        bool IsOnOwnProfilePage () => IsOnProfilePage () && GetUserProfileId ().Value == UserId;

        void ShowMyContent ()
        {
            ShowContent (UserId);
        }

        void ShowContent (int userId)
        {
            var dataProvider = new UserHtmlDataProvider ();
            var userHtml = dataProvider.GetUserHtml (userId, ModuleId);
            if (userHtml != null && !string.IsNullOrEmpty (userHtml.UserHtml)) {
                litUserHtml.Text = HttpUtility.HtmlDecode (userHtml.UserHtml);
            }
            else {
                litUserHtml.Text = HttpUtility.HtmlDecode (Settings.EmptyHtml);
            }
        }

        void ShowEditPanel ()
        {
            pnlUser.Visible = true;
            TryRestoreState ();
        }

        void HideModule ()
        {
            ContainerControl.Visible = false;
        }

        #region IActionable implementation

        public ModuleActionCollection ModuleActions {
            get {
                var actions = new ModuleActionCollection ();
                actions.Add (
                    GetNextActionID (),
                    LocalizeString ("AddUserHtml_Action.Text"),
                    ModuleActionType.AddContent,
                    "",
                    IconController.IconURL ("Add"),
                    EditUrl ("Edit"),
                    false,
                    SecurityAccessLevel.Edit,
                    true,
                    false
                );
                return actions;
            }
        }

        #endregion

        protected void btnSearchUser_Click (object sender, EventArgs e)
        {
            var searchText = txtSearchUser.Text.Trim ();
            btnSearchUser_Click_Internal (searchText, true);
            SessionSearchText = searchText;
        }

        void btnSearchUser_Click_Internal (string searchText, bool selectFirst)
        {
            var users = SearchUsers (searchText);
            if (users != null && users.Any ()) {
                pnlSelectUser.Visible = true;
                lblSearchResult.Text = string.Format (LocalizeString ("UsersFound_Format.Text"), users.Count ());
                selUser.DataSource = users.OrderBy (u => u.DisplayName)
                    .Select (u => new UserViewModel (u));
                selUser.DataBind ();

                if (selectFirst) {
                    selUser.SelectedIndex = 0;
                    var userId = TypeUtils.ParseToNullable<int> (selUser.SelectedValue, true);
                    selUser_SelectedIndexChanged_Internal (userId);
                }
            }
            else {
                pnlSelectUser.Visible = false;
                lblSearchResult.Text = LocalizeString ("NoUsersFound.Text");
            }
        }

        // TODO: Move to BLL
        // TODO: Rename to FindUsers
        // TODO: Also search by FirstName/LastName combinations
        IEnumerable<UserInfo> SearchUsers (string searchText)
        {
            var searchTextLC = searchText.ToLower ();

            return UserController.GetUsers (false, false, PortalId)
                                 .Cast<UserInfo> ()
                                 .Where (u =>
                                         (u.Email != null && u.Email.ToLower ().Contains (searchTextLC)) ||
                                         (u.Username != null && u.Username.ToLower ().Contains (searchTextLC)) ||
                                         (u.DisplayName != null && u.DisplayName.ToLower ().Contains (searchTextLC)) ||
                                         (u.LastName != null && u.LastName.ToLower ().Contains (searchTextLC)) ||
                                         (u.FirstName != null && u.FirstName.ToLower ().Contains (searchTextLC))
                                        );
        }

        protected void selUser_SelectedIndexChanged (object sender, EventArgs e)
        {
            var userId = TypeUtils.ParseToNullable<int> (selUser.SelectedValue, true);
            selUser_SelectedIndexChanged_Internal (userId);
            SessionUserId = userId;
        }

        void selUser_SelectedIndexChanged_Internal (int? userId)
        {
            if (userId != null) {
                lnkEditUserHtml.Visible = true;
                lnkEditUserHtml.NavigateUrl = EditUrl ("user_id", userId.ToString (), "Edit");

                ShowContent (userId.Value);
            }
            else {
                litUserHtml.Text = string.Empty;
                lnkEditUserHtml.Visible = false;
            }
        }
    }
}
