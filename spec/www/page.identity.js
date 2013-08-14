// Copyright (c) 2012 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

chromespec.registerSubPage('chrome.identity', function(rootEl) {
  function addButton(name, func) {
    var button = chromespec.createButton(name, func);
    rootEl.appendChild(button);
  }

  addButton('Get auth token', function() {
    var onGetAuthTokenSuccess = function(token) {
      chromespec.log('Token: ' + token);
    };

    chrome.identity.getAuthToken({ interactive: true }, onGetAuthTokenSuccess);
  });

  addButton('Remove cached auth token', function() {
    var onRemoveCachedAuthTokenSuccess = function() {
      chromespec.log('Token removed from cache.');
    };

    var onInitialGetAuthTokenSuccess = function(token) {
      chromespec.log('Removing token ' + token + ' from cache.');

      // Remove the token!
      chrome.identity.removeCachedAuthToken({ token: token }, onRemoveCachedAuthTokenSuccess);
    };

    // First, we need to get the existing auth token.
    chrome.identity.getAuthToken({ interactive: true }, onInitialGetAuthTokenSuccess);
  });

  addButton('Launch Google web auth flow', function() {
    chromespec.log('launchWebAuthFlow (google.com): Waiting for callback');

    var webAuthDetails = {
      interactive: true,
      url: 'https://accounts.google.com/o/oauth2/auth?client_id=429153676186-efnn5o5otvpa75kpa82ee91qkd80evb3.apps.googleusercontent.com&redirect_uri=http%3A%2F%2Fwww.google.com&response_type=token&scope=https%3A%2F%2Fwww.googleapis.com/auth/userinfo.profile'
    };

    var onLaunchWebAuthFlowSuccess = function(url) {
      chromespec.log('Resulting URL: ' + url);
    };

    chrome.identity.launchWebAuthFlow(webAuthDetails, onLaunchWebAuthFlowSuccess);
  });

  addButton('Launch Facebook web auth flow', function() {
    var FACEBOOK_PERMISSIONS='email';
    var FACEBOOK_APP_ID='218307504870310';
    var APPURL='https://'+chrome.runtime.id+'.chromiumapp.org/';
    var FACEBOOK_LOGIN_SUCCESS_URL= 'http://www.any.do/facebook_proxy/login_success?redirect='+encodeURIComponent(APPURL);
    var FACEBOOK_OAUTH_URL = 'http://www.facebook.com/dialog/oauth?display=popup&scope='+FACEBOOK_PERMISSIONS+'&client_id='+FACEBOOK_APP_ID+'&redirect_uri='+FACEBOOK_LOGIN_SUCCESS_URL;

    var webAuthDetails = {
        interactive: true,
        url:FACEBOOK_OAUTH_URL,
    };

    var onLaunchWebAuthFlowSuccess = function(url) {
        chromespec.log('Resulting URL: ' + url);
    };

    chrome.identity.launchWebAuthFlow(webAuthDetails, onLaunchWebAuthFlowSuccess);
  });
});

