// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Update notification system with changelog functionality
const UpdateNotification = {
    checkInterval: 24 * 60 * 60 * 1000, // 24 hours
    lastCheckKey: 'thmi_last_update_check',
    updateAvailableKey: 'thmi_update_available',
    updateDataKey: 'thmi_update_data',
    changelogCacheKey: 'thmi_changelog_cache',
    changelogCacheExpiry: 24 * 60 * 60 * 1000, // 24 hours cache
    
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
                try {
                    const updateData = JSON.parse(localStorage.getItem(this.updateDataKey) || '{}');
                    this.showUpdateNotification(updateData);
                } catch (e) {
                    console.warn('Failed to parse cached update data:', e);
                    localStorage.removeItem(this.updateDataKey);
                }
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
        fetch(window.location.origin + '/api/update/check-program', {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json'
            }
        })
        .then(response => {
            if (!response.ok) {
                throw new Error('HTTP error! status: ' + response.status);
            }
            return response.text();
        })
        .then(text => {
            localStorage.setItem(this.lastCheckKey, Date.now().toString());
            try {
                const data = JSON.parse(text);
                if (data.success && data.isUpdateAvailable) {
                    localStorage.setItem(this.updateAvailableKey, 'true');
                    localStorage.setItem(this.updateDataKey, JSON.stringify(data));
                    this.showUpdateNotification(data);
                } else {
                    localStorage.setItem(this.updateAvailableKey, 'false');
                    localStorage.removeItem(this.updateDataKey);
                    this.hideUpdateNotification();
                }
            } catch (parseError) {
                console.warn('Failed to parse update check response:', parseError);
                localStorage.setItem(this.updateAvailableKey, 'false');
                localStorage.removeItem(this.updateDataKey);
            }
        })
        .catch(error => {
            console.warn('Automatic update check failed:', error);
        });
    },
    
    showUpdateNotification: function(updateData) {
        const banner = document.getElementById('updateNotificationBanner');
        const text = document.getElementById('updateNotificationText');
        const downloadLink = document.getElementById('updateDownloadLink');
        const downloadText = document.getElementById('updateDownloadText');
        const settingsBadge = document.getElementById('settingsUpdateBadge');
        const settingsBadgeOffcanvas = document.getElementById('settingsUpdateBadgeOffcanvas');
        const changelogBtn = document.getElementById('updateChangelogBtn');
        
        if (banner && text && downloadLink && downloadText) {
            text.textContent = getLocalizedString('Updates:UpdateAvailableBanner', 'Update available: Version {0}').replace('{0}', updateData.latestVersion);
            downloadLink.href = updateData.downloadUrl;
            downloadText.textContent = getLocalizedString('Common:Download', 'Download');
            
            // Show changelog button if we have release notes
            if (changelogBtn && updateData.releaseNotes) {
                changelogBtn.style.display = 'inline-flex';
                changelogBtn.dataset.version = updateData.latestVersion;
            }
            
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
        const changelogBtn = document.getElementById('updateChangelogBtn');
        
        if (banner) {
            banner.style.display = 'none';
        }
        
        // Hide notification badges
        if (settingsBadge) settingsBadge.style.display = 'none';
        if (settingsBadgeOffcanvas) settingsBadgeOffcanvas.style.display = 'none';
        
        // Hide changelog button
        if (changelogBtn) changelogBtn.style.display = 'none';
    }
};

// Changelog modal functionality
const ChangelogModal = {
    cacheKey: 'thmi_changelog_cache',
    cacheExpiry: 24 * 60 * 60 * 1000, // 24 hours
    
    init: function() {
        const modal = document.getElementById('changelogModal');
        if (modal) {
            modal.addEventListener('show.bs.modal', () => {
                // Clear previous content when opening
                const contentEl = document.getElementById('changelogModalContent');
                const loadingEl = document.getElementById('changelogModalLoading');
                const errorEl = document.getElementById('changelogModalError');
                const versionEl = document.getElementById('changelogModalVersion');
                
                if (contentEl) contentEl.innerHTML = '';
                if (loadingEl) loadingEl.style.display = 'flex';
                if (errorEl) errorEl.style.display = 'none';
                if (versionEl) versionEl.textContent = '';
            });
        }
        
        // Handle changelog button click
        document.addEventListener('click', (e) => {
            const btn = e.target.closest('#updateChangelogBtn');
            if (btn) {
                const version = btn.dataset.version;
                this.showChangelog(version);
            }
        });
    },
    
    showChangelog: async function(version) {
        const modal = new bootstrap.Modal(document.getElementById('changelogModal'));
        const loadingEl = document.getElementById('changelogModalLoading');
        const errorEl = document.getElementById('changelogModalError');
        const contentEl = document.getElementById('changelogModalContent');
        const versionEl = document.getElementById('changelogModalVersion');
        const errorMessageEl = document.getElementById('changelogErrorMessage');
        
        // Show modal
        modal.show();
        
        // Update version display
        if (versionEl) {
            versionEl.textContent = version || '';
        }
        
        // Check cache first
        const cachedData = this.getCachedChangelog(version);
        if (cachedData) {
            this.displayChangelog(cachedData);
            return;
        }
        
        try {
            // Show loading state
            if (loadingEl) loadingEl.style.display = 'flex';
            if (errorEl) errorEl.style.display = 'none';
            if (contentEl) contentEl.innerHTML = '';
            
            // Fetch from API
            const response = await fetch(`/api/whatsnew/release-notes?version=${encodeURIComponent(version)}`);
            const data = await response.json();
            
            if (data.success) {
                // Cache the result
                this.cacheChangelog(version, data);
                
                // Display the changelog
                this.displayChangelog(data);
                
                // Hide loading
                if (loadingEl) loadingEl.style.display = 'none';
            } else {
                throw new Error(data.message || 'Failed to fetch changelog');
            }
        } catch (error) {
            console.error('Error fetching changelog:', error);
            
            // Show error state
            if (loadingEl) loadingEl.style.display = 'none';
            if (errorEl) errorEl.style.display = 'flex';
            if (errorMessageEl) {
                errorMessageEl.textContent = getLocalizedString(
                    'Updates:ChangelogFetchError', 
                    'Failed to load changelog. Please try again later.'
                );
            }
        }
    },
    
    displayChangelog: function(data) {
        const loadingEl = document.getElementById('changelogModalLoading');
        const errorEl = document.getElementById('changelogModalError');
        const contentEl = document.getElementById('changelogModalContent');
        const versionEl = document.getElementById('changelogModalVersion');
        const publishedEl = document.getElementById('changelogModalPublished');
        
        // Hide loading and error
        if (loadingEl) loadingEl.style.display = 'none';
        if (errorEl) errorEl.style.display = 'none';
        
        // Update version info
        if (versionEl) {
            versionEl.textContent = data.version || '';
        }
        
        // Update published date
        if (publishedEl && data.publishedAt) {
            const date = new Date(data.publishedAt);
            publishedEl.textContent = date.toLocaleDateString(getLocalizedString('Common:DateFormat', 'en-US'), {
                year: 'numeric',
                month: 'long',
                day: 'numeric'
            });
        }
        
        // Display content
        if (contentEl && data.releaseNotes) {
            contentEl.innerHTML = data.releaseNotes;
        }
    },
    
    cacheChangelog: function(version, data) {
        try {
            const cache = {
                version: version,
                data: data,
                timestamp: Date.now()
            };
            localStorage.setItem(`${this.cacheKey}_${version}`, JSON.stringify(cache));
        } catch (e) {
            console.warn('Failed to cache changelog:', e);
        }
    },
    
    getCachedChangelog: function(version) {
        try {
            const cached = localStorage.getItem(`${this.cacheKey}_${version}`);
            if (cached) {
                const cache = JSON.parse(cached);
                // Check if cache is still valid
                if (Date.now() - cache.timestamp < this.cacheExpiry) {
                    return cache.data;
                }
            }
        } catch (e) {
            console.warn('Failed to get cached changelog:', e);
        }
        return null;
    },
    
    clearCache: function() {
        try {
            Object.keys(localStorage).forEach(key => {
                if (key.startsWith(this.cacheKey)) {
                    localStorage.removeItem(key);
                }
            });
        } catch (e) {
            console.warn('Failed to clear changelog cache:', e);
        }
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

// Initialize update notifications and changelog modal when DOM is ready (delayed to not block page render)
document.addEventListener('DOMContentLoaded', function() {
    // Initialize icon font
    initIconFont();
    
    // Initialize changelog modal functionality
    if (typeof ChangelogModal !== 'undefined') {
        ChangelogModal.init();
    }
    
    // Delay update check to allow page to render first
    // This reduces perceived load time and avoids conflicts with browser extensions
    if (window.requestIdleCallback) {
        requestIdleCallback(function() {
            UpdateNotification.init();
        }, { timeout: 5000 });
    } else {
        setTimeout(function() {
            UpdateNotification.init();
        }, 1000);
    }
});

// Initialize icon font
function initIconFont() {
    const savedIconFont = localStorage.getItem('iconFont') || 'mdl2';
    updateIconFont(savedIconFont);
}

// Update icon font function
window.updateIconFont = function(fontValue) {
    const root = document.documentElement;
    if (fontValue === 'fluent') {
        root.style.setProperty('--icon-font-family', "'Segoe Fluent Icons', 'Segoe MDL2 Assets', sans-serif");
    } else {
        root.style.setProperty('--icon-font-family', "'Segoe MDL2 Assets', 'Segoe Fluent Icons', sans-serif");
    }
    // Update hidden input for form submission (if on settings page)
    const hiddenInput = document.getElementById('iconFontHidden');
    if (hiddenInput) {
        hiddenInput.value = fontValue;
    }
    // Save to localStorage for persistence
    localStorage.setItem('iconFont', fontValue);
    console.log('Icon font changed to:', fontValue);
};

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
