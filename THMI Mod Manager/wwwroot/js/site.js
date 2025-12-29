// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Update notification system
const UpdateNotification = {
    checkInterval: 24 * 60 * 60 * 1000, // 24 hours
    lastCheckKey: 'thmi_last_update_check',
    updateAvailableKey: 'thmi_update_available',
    updateDataKey: 'thmi_update_data',
    
    init: function() {
        // Check if we should perform an automatic update check
        const lastCheck = localStorage.getItem(this.lastCheckKey);
        const now = Date.now();
        
        if (!lastCheck || (now - parseInt(lastCheck)) > this.checkInterval) {
            this.performUpdateCheck();
        } else {
            // Check if there's a cached update notification
            const updateAvailable = localStorage.getItem(this.updateAvailableKey);
            if (updateAvailable === 'true') {
                const updateData = JSON.parse(localStorage.getItem(this.updateDataKey) || '{}');
                this.showUpdateNotification(updateData);
            }
        }
        
        // Add event listener for manual update checks from settings page
        document.addEventListener('updateCheckCompleted', (event) => {
            if (event.detail.updateAvailable) {
                this.showUpdateNotification(event.detail);
            }
        });
    },
    
    performUpdateCheck: function() {
        fetch('/api/update/check', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
            }
        })
        .then(response => response.json())
        .then(data => {
            localStorage.setItem(this.lastCheckKey, Date.now().toString());
            
            if (data.updateAvailable) {
                localStorage.setItem(this.updateAvailableKey, 'true');
                localStorage.setItem(this.updateDataKey, JSON.stringify(data));
                this.showUpdateNotification(data);
            } else {
                localStorage.setItem(this.updateAvailableKey, 'false');
                localStorage.removeItem(this.updateDataKey);
                this.hideUpdateNotification();
            }
        })
        .catch(error => {
            console.error('Automatic update check failed:', error);
            // Don't show error for automatic checks, just try again later
        });
    },
    
    showUpdateNotification: function(updateData) {
        const banner = document.getElementById('updateNotificationBanner');
        const text = document.getElementById('updateNotificationText');
        const downloadLink = document.getElementById('updateDownloadLink');
        const downloadText = document.getElementById('updateDownloadText');
        const settingsBadge = document.getElementById('settingsUpdateBadge');
        const settingsBadgeOffcanvas = document.getElementById('settingsUpdateBadgeOffcanvas');
        
        if (banner && text && downloadLink && downloadText) {
            text.textContent = getLocalizedString('Updates:UpdateAvailableBanner', 'Update available: Version {0}').replace('{0}', updateData.latestVersion);
            downloadLink.href = updateData.downloadUrl;
            downloadText.textContent = getLocalizedString('Common:Download', 'Download');
            
            banner.style.display = 'block';
            
            // Show notification badges on settings menu
            if (settingsBadge) settingsBadge.style.display = 'inline-block';
            if (settingsBadgeOffcanvas) settingsBadgeOffcanvas.style.display = 'inline-block';
            
            // Add animation
            setTimeout(() => {
                banner.classList.add('show');
            }, 100);
        }
    },
    
    hideUpdateNotification: function() {
        const banner = document.getElementById('updateNotificationBanner');
        const settingsBadge = document.getElementById('settingsUpdateBadge');
        const settingsBadgeOffcanvas = document.getElementById('settingsUpdateBadgeOffcanvas');
        
        if (banner) {
            banner.style.display = 'none';
        }
        
        // Hide notification badges
        if (settingsBadge) settingsBadge.style.display = 'none';
        if (settingsBadgeOffcanvas) settingsBadgeOffcanvas.style.display = 'none';
    }
};

// Helper function to get localized strings (fallback to English)
function getLocalizedString(key, defaultValue) {
    // Try to get from the AppConfig if available, otherwise use default
    if (window.AppConfig && window.AppConfig.getLocalized) {
        return window.AppConfig.getLocalized(key, defaultValue);
    }
    return defaultValue;
}

// Initialize update notifications when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    UpdateNotification.init();
});

// Global function to manually check for updates
window.checkForUpdates = function() {
    UpdateNotification.performUpdateCheck();
};

// Toast notification functionality
const ToastNotification = {
    showUpdateToast: function(updateData) {
        const toastElement = document.getElementById('updateToast');
        const toastMessage = document.getElementById('updateToastMessage');
        const toastDownload = document.getElementById('updateToastDownload');
        
        if (toastElement && toastMessage && toastDownload) {
            toastMessage.textContent = getLocalizedString('Updates:UpdateNotificationMessage', 'A new version {0} is available. Would you like to update now?').replace('{0}', updateData.latestVersion);
            
            toastDownload.onclick = function() {
                window.open(updateData.downloadUrl, '_blank');
            };
            
            const toast = new bootstrap.Toast(toastElement, {
                autohide: false,
                delay: 0
            });
            
            toast.show();
        }
    }
};

// Extend UpdateNotification to also show toast notifications
const originalShowUpdateNotification = UpdateNotification.showUpdateNotification;
UpdateNotification.showUpdateNotification = function(updateData) {
    // Call original function to show banner
    originalShowUpdateNotification.call(this, updateData);
    
    // Also show toast notification (but only once per session)
    const toastShownKey = 'thmi_update_toast_shown_' + updateData.latestVersion;
    if (!sessionStorage.getItem(toastShownKey)) {
        ToastNotification.showUpdateToast(updateData);
        sessionStorage.setItem(toastShownKey, 'true');
    }
};
