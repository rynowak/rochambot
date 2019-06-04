(function () {
    var userInfoComponent;

    window.openTwitterLoginPopup = function(component) {
        window.openLoginPopup(component, "twitter");
    };
    
    window.openMicrosoftLoginPopup = function(component) {
        window.openLoginPopup(component, "microsoft");
    };
    
    window.openLoginPopup = function (component, provider) {
        console.log(provider);

        if (userInfoComponent) {
            userInfoComponent.dispose();
        }

        userInfoComponent = component;
        var popup = window.open('user/signin/' + provider + '?returnUrl=' + encodeURIComponent(location.href), 'loginWindow', 'width=600,height=600');

        // Poll to see if it's closed before completion
        var intervalHandle = setInterval(function () {
            if (popup.closed) {
                clearInterval(intervalHandle);
                onLoginPopupFinished({ isLoggedIn: false });
            }
        }, 250);
    };

    window.onLoginPopupFinished = function (userState) {
        if (userInfoComponent) {
            userInfoComponent.invokeMethodAsync('OnSignInStateChanged', userState);
            userInfoComponent.dispose();
            userInfoComponent = null;
        }
    };
})();