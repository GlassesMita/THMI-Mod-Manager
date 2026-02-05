window.saveBepInExSettings = function() {
    const saveButton = document.getElementById('saveBepInExSettings');
    if (!saveButton) return;
    
    const localizedSave = document.getElementById('localizedSave')?.value || 'Save';
    const localizedSaving = document.getElementById('localizedSaving')?.value || 'Saving...';
    
    saveButton.disabled = true;
    saveButton.innerHTML = '<span class="spinner-border spinner-border-sm"></span> ' + localizedSaving;
    
    const form = document.getElementById('SettingsForm');
    if (!form) {
        console.error('Settings form not found');
        saveButton.disabled = false;
        saveButton.innerHTML = '<span style="font-family: \'Segoe Fluent Icons\'; margin-right: 4px;">&#xE74E;</span> ' + localizedSave;
        return;
    }
    
    const formData = new FormData();
    
    const token = document.querySelector('input[name="__RequestVerificationToken"]');
    if (token) {
        formData.append('__RequestVerificationToken', token.value);
    }
    
    // Process all BepInEx setting inputs dynamically
    const allInputs = document.querySelectorAll('#SettingsForm input, #SettingsForm select, #SettingsForm textarea');
    allInputs.forEach(input => {
        // Check if this is a BepInEx setting field (has an ID with underscore pattern)
        if (input.id && input.id.includes('_') && input.name) {
            // Special handling for checkbox inputs
            if (input.type === 'checkbox') {
                // Send the actual checked state as a boolean
                formData.append(input.name, input.checked);
            } else {
                // For other input types, send the value
                formData.append(input.name, input.value);
            }
        }
    });
    
    // Handle the config path field separately (it doesn't have underscore pattern)
    const bepInExConfigPath = document.getElementById('bepInExConfigPath');
    if (bepInExConfigPath) {
        formData.append('bepInExConfigPath', bepInExConfigPath.value);
    }
    
    fetch('/settings?handler=SaveBepInExSettings', {
        method: 'POST',
        body: formData,
        headers: {
            'RequestVerificationToken': token?.value || ''
        }
    })
    .then(response => {
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        return response.json();
    })
    .then(data => {
        saveButton.disabled = false;
        saveButton.innerHTML = '<span style="font-family: \'Segoe Fluent Icons\'; margin-right: 4px;">&#xE74E;</span> ' + localizedSave;
        
        if (data.success) {
            const successToast = document.getElementById('successToast');
            if (successToast) {
                successToast.classList.add('show');
                setTimeout(() => successToast.classList.remove('show'), 3000);
            }
        } else {
            ModalUtils.alert('保存失败', data.message || '保存 BepInEx 设置失败，请重试。');
        }
    })
    .catch(error => {
        console.error('保存 BepInEx 设置失败:', error);
        saveButton.disabled = false;
        saveButton.innerHTML = '<span style="font-family: \'Segoe Fluent Icons\'; margin-right: 4px;">&#xE74E;</span> ' + localizedSave;
        ModalUtils.alert('保存失败', '保存 BepInEx 设置失败，请重试: ' + error.message);
    });
}

window.resetBepInExSettings = function() {
    const localizedResetConfirm = document.getElementById('localizedResetConfirm')?.value || '确定要将所有 BepInEx 设置恢复为默认值吗？注意：恢复默认值后，请点击保存按钮来应用更改。当前配置文件的内容将被覆盖。';
    const localizedResetComplete = document.getElementById('localizedResetComplete')?.value || '已恢复默认值，请点击保存按钮来应用更改。';
    
    if (!confirm(localizedResetConfirm)) {
        return;
    }
    
    const defaultValues = {
        'Caching_EnableAssemblyCache': true,
        'Detours_DetourProviderType': 'Default',
        'Harmony.Logger_LogChannels': 'Warn, Error',
        'IL2CPP_UpdateInteropAssemblies': true,
        'IL2CPP_UnityBaseLibrariesSource': 'https://unity.bepinex.dev/libraries/{VERSION}.zip',
        'IL2CPP_IL2CPPInteropAssembliesPath': '{BepInEx}',
        'IL2CPP_PreloadIL2CPPInteropAssemblies': true,
        'Logging_UnityLogListening': true,
        'Logging.Console_Enabled': true,
        'Logging.Console_PreventClose': false,
        'Logging.Console_ShiftJisEncoding': false,
        'Logging.Console_StandardOutType': 'Auto',
        'Logging.Console_LogLevels': 'Fatal, Error, Warning, Message, Info',
        'Logging.Disk_Enabled': true,
        'Logging.Disk_AppendLog': false,
        'Logging.Disk_LogLevels': 'Fatal, Error, Warning, Message, Info',
        'Logging.Disk_InstantFlushing': false,
        'Logging.Disk_ConcurrentFileLimit': 5,
        'Logging.Disk_WriteUnityLog': false,
        'Preloader_HarmonyBackend': 'auto',
        'Preloader_DumpAssemblies': false,
        'Preloader_LoadDumpedAssemblies': false,
        'Preloader_BreakBeforeLoadAssemblies': false
    };
    
    Object.entries(defaultValues).forEach(([fieldId, value]) => {
        const element = document.getElementById(fieldId);
        if (element) {
            if (element.type === 'checkbox') {
                element.checked = value;
            } else {
                element.value = value;
            }
        }
    });
    
    const successToast = document.getElementById('successToast');
    if (successToast) {
        const successToastBody = successToast.querySelector('.toast-body');
        if (successToastBody) {
            successToastBody.textContent = localizedResetComplete;
        }
        successToast.classList.add('show');
        setTimeout(() => successToast.classList.remove('show'), 5000);
    }
}

document.addEventListener('DOMContentLoaded', function() {
    const form = document.getElementById('SettingsForm');
    const successToast = document.getElementById('successToast');
    const selects = document.querySelectorAll('.cdx-select');
    let isSaving = false;
    
    // 如果表单不存在，直接返回
    if (!form) {
        console.log('Settings form not found, skipping settings.js initialization');
        return;
    }

    // BepInEx 按钮事件监听器
    const saveBepInExBtn = document.getElementById('saveBepInExSettings');
    const resetBepInExBtn = document.getElementById('resetBepInExSettings');
    const saveAllBtn = document.getElementById('saveButton');

    if (saveBepInExBtn) {
        saveBepInExBtn.addEventListener('click', function() {
            if (typeof window.saveBepInExSettings === 'function') {
                window.saveBepInExSettings();
            } else {
                console.error('saveBepInExSettings function not found');
            }
        });
    }

    if (resetBepInExBtn) {
        resetBepInExBtn.addEventListener('click', function() {
            if (typeof window.resetBepInExSettings === 'function') {
                window.resetBepInExSettings();
            } else {
                console.error('resetBepInExSettings function not found');
            }
        });
    }

    if (saveAllBtn) {
        saveAllBtn.addEventListener('click', function() {
            if (typeof window.saveAllSettings === 'function') {
                window.saveAllSettings();
            } else {
                console.error('saveAllSettings function not found');
            }
        });
    }

    const saveAllBtnTop = document.getElementById('saveButtonTop');
    if (saveAllBtnTop) {
        saveAllBtnTop.addEventListener('click', function() {
            if (typeof window.saveAllSettings === 'function') {
                window.saveAllSettings();
            } else {
                console.error('saveAllSettings function not found');
            }
        });
    }

    // Load saved icon font on page load
    const savedIconFont = localStorage.getItem('iconFont');
    if (savedIconFont) {
        const iconFontSelect = document.getElementById('iconFont');
        if (iconFontSelect) {
            iconFontSelect.value = savedIconFont;
        }
        // Call global updateIconFont function from site.js
        if (typeof window.updateIconFont === 'function') {
            window.updateIconFont(savedIconFont);
        }
    } else {
        // Initialize with default value if no saved font
        const iconFontSelect = document.getElementById('iconFont');
        if (iconFontSelect && typeof window.updateIconFont === 'function') {
            window.updateIconFont(iconFontSelect.value);
        }
    }

    const simpleMarkdownParser = (text) => {
        if (!text) return '';
        let html = text
            .replace(/^### (.+)$/gm, '<h3>$1</h3>')
            .replace(/^## (.+)$/gm, '<h2>$1</h2>')
            .replace(/^# (.+)$/gm, '<h1>$1</h1>')
            .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
            .replace(/\*(.+?)\*/g, '<em>$1</em>')
            .replace(/`(.+?)`/g, '<code>$1</code>')
            .replace(/```(\w+)?\n([\s\S]*?)```/g, '<pre><code class="language-$1">$2</code></pre>')
            .replace(/^\- (.+)$/gm, '<li>$1</li>')
            .replace(/(<li>.*<\/li>\n?)+/g, '<ul>$&</ul>')
            .replace(/\n\n/g, '</p><p>')
            .replace(/^(?!<[hulop])/gm, '<p>')
            .replace(/(?<![>])$/gm, '</p>');
        return html;
    };
    
    // 为浏览按钮添加事件监听器
    const browseLauncherPath = document.getElementById('browseLauncherPath');
    if (browseLauncherPath) {
        browseLauncherPath.addEventListener('click', () => openFileBrowser('executable'));
    }
    
    // 为所有下拉框添加动画效果
    selects.forEach((select, index) => {
        select.addEventListener('change', function() {
            this.style.borderColor = 'var(--color-success)';
            setTimeout(() => {
                this.style.borderColor = '';
            }, 1500);
        });
    });
    
    form.addEventListener('submit', function(e) {
        e.preventDefault();
        
        if (isSaving) {
            console.log('Settings already being saved, ignoring duplicate submission');
            return;
        }
        isSaving = true;
        
        const submitButton = form.querySelector('.cdx-button--primary');
        const saveText = document.getElementById('saveText');
        const loadingSpinner = document.getElementById('loadingSpinner');
        
        if (submitButton) {
            submitButton.classList.add('loading');
            submitButton.disabled = true;
        }
        if (saveText) saveText.style.display = 'none';
        if (loadingSpinner) loadingSpinner.style.display = 'inline-block';
        
        // 创建表单数据
        const formData = new FormData(form);
        
        // 确保包含所有必要的字段
        const modifyTitle = document.getElementById('modifyTitle').checked;
        formData.set('modifyTitle', modifyTitle);
        
        // 确保 autoCheckUpdates 字段被正确包含
        const autoCheckUpdatesCheckbox = document.getElementById('autoCheckUpdates');
        if (autoCheckUpdatesCheckbox) {
            const autoCheckUpdatesValue = autoCheckUpdatesCheckbox.checked;
            formData.set('autoCheckUpdates', autoCheckUpdatesValue);
            console.log('autoCheckUpdates value:', autoCheckUpdatesValue);
        }
        
        // 确保 updateFrequency 字段被正确包含
        const updateFrequencySelect = document.getElementById('updateFrequency');
        if (updateFrequencySelect) {
            const updateFrequencyValue = updateFrequencySelect.value;
            formData.set('updateFrequency', updateFrequencyValue);
            console.log('updateFrequency value:', updateFrequencyValue);
        }
        
        // 确保 enableNotifications 字段被正确包含
        const enableNotificationsCheckbox = document.getElementById('enableNotifications');
        if (enableNotificationsCheckbox) {
            const enableNotificationsValue = enableNotificationsCheckbox.checked;
            formData.set('enableNotifications', enableNotificationsValue);
            console.log('enableNotifications value:', enableNotificationsValue);
        }
        
        // 确保 showSeconds 字段被正确包含
        const showSecondsCheckbox = document.getElementById('showSeconds');
        if (showSecondsCheckbox) {
            const showSecondsValue = showSecondsCheckbox.checked;
            formData.set('showSeconds', showSecondsValue);
            console.log('showSeconds value:', showSecondsValue);
        }
        
        // 确保 use12Hour 字段被正确包含
        const use12HourCheckbox = document.getElementById('use12Hour');
        if (use12HourCheckbox) {
            const use12HourValue = use12HourCheckbox.checked;
            formData.set('use12Hour', use12HourValue);
            console.log('use12Hour value:', use12HourValue);
        }
        
        // 确保 dateFormat 字段被正确包含
        const dateFormatSelect = document.getElementById('dateFormat');
        if (dateFormatSelect) {
            const dateFormatValue = dateFormatSelect.value;
            formData.set('dateFormat', dateFormatValue);
            console.log('dateFormat value:', dateFormatValue);
        }
        
        // 发送到后端保存
        fetch('/settings?handler=SaveLanguage', {
            method: 'POST',
            body: formData,
            headers: {
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            }
        })
        .then(response => {
            console.log('Response status:', response.status);
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            const contentType = response.headers.get('content-type');
            if (contentType && contentType.includes('application/json')) {
                return response.json();
            }
            return response.text();
        })
        .then(data => {
            console.log('Response data:', data);
            
            // 移除加载动画
            if (submitButton) {
                submitButton.classList.remove('loading');
                submitButton.disabled = false;
            }
            if (saveText) saveText.style.display = 'inline';
            if (loadingSpinner) loadingSpinner.style.display = 'none';
            isSaving = false;
            
            // 显示成功提示
            if (successToast) successToast.classList.add('show');
            
            // 刷新自定义光标设置
            if (typeof window.refreshCustomCursor === 'function') {
                window.refreshCustomCursor();
            }
            
            // 3秒后隐藏成功提示
            setTimeout(function() {
                successToast.classList.remove('show');
            }, 3000);
            
            // 重新加载页面以显示更新后的设置
            setTimeout(function() {
                window.location.reload();
            }, 1500);
        })
        .catch(error => {
            console.error('保存设置失败:', error);
            isSaving = false;
            
            // 移除加载动画
            if (submitButton) {
                submitButton.classList.remove('loading');
                submitButton.disabled = false;
            }
            if (saveText) saveText.style.display = 'inline';
            if (loadingSpinner) loadingSpinner.style.display = 'none';
            
            // 显示错误提示
            ModalUtils.alert('保存失败', '保存设置失败，请重试: ' + error.message);
        });
    });
    
    const cursorRadios = document.querySelectorAll('input[name="cursorType"]');
    cursorRadios.forEach(radio => {
        radio.addEventListener('change', function() {
            document.querySelectorAll('.cdx-radio-container').forEach(option => {
                option.style.backgroundColor = 'transparent';
            });
            
            if (this.checked) {
                const themeColor = document.getElementById('colorPreviewText') ? document.getElementById('colorPreviewText').textContent : '#c670ff';
                this.closest('.cdx-radio-container').style.backgroundColor = 'rgba(' + parseInt(themeColor.substring(1, 3), 16) + ', ' + parseInt(themeColor.substring(3, 5), 16) + ', ' + parseInt(themeColor.substring(5, 7), 16) + ', 0.1)';
            }
        });
    });
    
    // 初始化主题颜色
    function initThemeColor() {
        const themeColor = document.getElementById('themeColorHidden') ? document.getElementById('themeColorHidden').value : '#c670ff';
        document.documentElement.style.setProperty('--theme-color', themeColor);
    }
    
    // 初始化时调用
    initThemeColor();

    // 标题点击事件 - 重定向到 Debug 页面
    const settingsTitle = document.getElementById('settingsTitle');
    const clickCounter = document.getElementById('clickCounter');
    const clickCountSpan = document.getElementById('clickCount');
    
    if (settingsTitle && clickCounter && clickCountSpan) {
        let clickCount = 0;
        
        settingsTitle.addEventListener('click', function() {
            clickCount++;
            clickCountSpan.textContent = clickCount;
            clickCounter.classList.add('show');
            
            if (clickCount >= 10) {
                window.location.href = '/DebugPage';
            }
            
            // 3秒后隐藏计数器
            setTimeout(function() {
                if (clickCount < 10) {
                    clickCounter.classList.remove('show');
                }
            }, 3000);
        });
    }

    // 主题色选择器
    const themeColorPicker = document.getElementById('themeColorPicker');
    const colorPreview = document.getElementById('colorPreview');
    const colorPreviewText = document.getElementById('colorPreviewText');

    if (themeColorPicker && colorPreview && colorPreviewText) {
        themeColorPicker.addEventListener('input', function() {
            const color = this.value;
            colorPreview.style.backgroundColor = color;
            colorPreviewText.textContent = color;
        });
    }

    // 初始化游戏启动模式设置
    const launchModeSteam = document.getElementById('launchModeSteam');
    const launchModeExternal = document.getElementById('launchModeExternal');
    const launcherPathSection = document.getElementById('launcherPathSection');
    
    // 确保正确的启动模式被选中
    if (launchModeSteam && launchModeExternal) {
        // 这些值将由Razor在页面加载时设置
        const launchMode = document.getElementById('launchModeHidden') ? document.getElementById('launchModeHidden').value : 'steam_launch';
        if (launchMode === 'steam_launch') {
            launchModeSteam.checked = true;
            launcherPathSection.style.display = 'none';
        } else {
            launchModeExternal.checked = true;
            launcherPathSection.style.display = 'block';
        }
    }

    // 更新检查功能
    const checkNowButton = document.getElementById('checkNowButton');
    const updateStatus = document.getElementById('updateStatus');
    const updateResult = document.getElementById('updateResult');
    const autoCheckUpdates = document.getElementById('autoCheckUpdates');

    if (checkNowButton && updateStatus && updateResult) {
        checkNowButton.addEventListener('click', function() {
            performUpdateCheck();
        });
    }

    function performUpdateCheck() {
        if (checkNowButton) checkNowButton.disabled = true;
        if (updateStatus) updateStatus.style.display = 'block';
        if (updateResult) updateResult.style.display = 'none';

        fetch(window.location.origin + '/api/update/check-program', {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            }
        })
        .then(response => response.json())
        .then(data => {
            if (updateStatus) updateStatus.style.display = 'none';
            if (updateResult) {
                updateResult.style.display = 'block';
                
                const localizedUpdateAvailable = document.getElementById('localizedUpdateAvailable')?.value || 'Update available: Version {0}';
                const localizedNoReleaseNotes = document.getElementById('localizedNoReleaseNotes')?.value || 'No release notes available';
                const localizedDownloadUpdate = document.getElementById('localizedDownloadUpdate')?.value || 'Download Update';
                const localizedNoUpdatesAvailable = document.getElementById('localizedNoUpdatesAvailable')?.value || 'No updates available. You are using the latest version.';
                const localizedUpdateCheckingDisabled = document.getElementById('localizedUpdateCheckingDisabled')?.value || 'Update checking is disabled';
                const localizedUpdateCheckFailed = document.getElementById('localizedUpdateCheckFailed')?.value || 'Update check failed';
                
                if (data.success && data.isUpdateAvailable) {
                    currentDownloadUrl = data.downloadUrl;
                    currentLatestVersion = data.latestVersion;
                    updateResult.innerHTML = `
                        <div class="alert alert-info">
                            <h6>${localizedUpdateAvailable.replace('{0}', data.latestVersion)}</h6>
                            <div class="release-notes">${simpleMarkdownParser(data.releaseNotes || localizedNoReleaseNotes)}</div>
                        </div>
                    `;
                    if (downloadUpdateButton) {
                        downloadUpdateButton.style.display = 'inline-block';
                    }
                    
                    const event = new CustomEvent('updateCheckCompleted', {
                        detail: data
                    });
                    document.dispatchEvent(event);
                } else if (data.success && !data.isUpdateAvailable) {
                    updateResult.innerHTML = `
                        <div class="alert alert-success">
                            ${localizedNoUpdatesAvailable}
                        </div>
                    `;
                } else if (data.updateCheckingDisabled) {
                    updateResult.innerHTML = `
                        <div class="alert alert-warning">
                            ${data.message || localizedUpdateCheckingDisabled}
                        </div>
                    `;
                } else {
                    updateResult.innerHTML = `
                        <div class="alert alert-danger">
                            ${localizedUpdateCheckFailed}: ${data.error || data.message}
                        </div>
                    `;
                }
            }
        })
        .catch(error => {
            console.error('Update check failed:', error);
            if (updateStatus) updateStatus.style.display = 'none';
            if (updateResult) {
                updateResult.style.display = 'block';
                const localizedUpdateCheckFailed = document.getElementById('localizedUpdateCheckFailed')?.value || 'Update check failed';
                updateResult.innerHTML = `
                    <div class="alert alert-danger">
                        ${localizedUpdateCheckFailed}: ${error.message}
                    </div>
                `;
            }
        })
        .finally(() => {
            if (checkNowButton) checkNowButton.disabled = false;
        });
    }

    // 自动检查更新设置变更
    if (autoCheckUpdates) {
        autoCheckUpdates.addEventListener('change', function() {
            console.log('Auto check updates setting changed to:', this.checked);
        });
    }
    
    // 初始化更新频率显示状态
    const updateFrequencySection = document.getElementById('updateFrequencySection');
    if (autoCheckUpdates && updateFrequencySection) {
        updateFrequencySection.style.display = autoCheckUpdates.checked ? 'block' : 'none';
    }

    // 下载更新按钮
    const downloadUpdateButton = document.getElementById('downloadUpdateButton');
    const applyUpdateButton = document.getElementById('applyUpdateButton');
    const updateProgress = document.getElementById('updateProgress');
    const updateProgressBar = document.getElementById('updateProgressBar');
    const updateProgressText = document.getElementById('updateProgressText');
    let currentDownloadUrl = null;
    let currentTempPath = null;
    let currentLatestVersion = null;

    if (downloadUpdateButton && applyUpdateButton) {
        downloadUpdateButton.addEventListener('click', async function() {
            if (!currentDownloadUrl) {
                console.error('Download URL not found');
                return;
            }

            downloadUpdateButton.disabled = true;
            downloadUpdateButton.innerHTML = '<span class="spinner-border spinner-border-sm"></span> ' + (document.getElementById('localizedDownloadingUpdate')?.value || 'Downloading...');

            if (updateProgress) updateProgress.style.display = 'block';
            if (updateProgressBar) updateProgressBar.style.width = '0%';
            if (updateProgressText) updateProgressText.textContent = '0%';

            try {
                const response = await fetch('/api/update/download', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({ downloadUrl: currentDownloadUrl })
                });

                const data = await response.json();

                if (data.success) {
                    currentTempPath = data.tempPath;
                    downloadUpdateButton.style.display = 'none';
                    applyUpdateButton.style.display = 'inline-block';
                    updateResult.innerHTML = `
                        <div class="alert alert-success">
                            ${document.getElementById('localizedDownloadComplete')?.value || 'Download complete! Click "Apply Update" to restart and update.'}
                        </div>
                    `;
                    if (updateProgress) updateProgress.style.display = 'none';
                } else {
                    downloadUpdateButton.disabled = false;
                    downloadUpdateButton.textContent = document.getElementById('localizedDownloadButton')?.value || 'Download Update';
                    updateResult.innerHTML = `
                        <div class="alert alert-danger">
                            ${document.getElementById('localizedDownloadFailed')?.value || 'Download failed'}: ${data.message}
                        </div>
                    `;
                }
            } catch (error) {
                console.error('Download failed:', error);
                downloadUpdateButton.disabled = false;
                downloadUpdateButton.textContent = document.getElementById('localizedDownloadButton')?.value || 'Download Update';
                updateResult.innerHTML = `
                    <div class="alert alert-danger">
                        ${document.getElementById('localizedDownloadFailed')?.value || 'Download failed'}: ${error.message}
                    </div>
                `;
            }
        });

        applyUpdateButton.addEventListener('click', async function() {
            if (!currentDownloadUrl) {
                console.error('Download URL not found');
                return;
            }

            applyUpdateButton.disabled = true;
            applyUpdateButton.innerHTML = '<span class="spinner-border spinner-border-sm"></span> ' + (document.getElementById('localizedApplyingUpdate')?.value || 'Applying update...');

            try {
                const prepareResponse = await fetch('/api/update/prepare', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({ 
                        downloadUrl: currentDownloadUrl,
                        newVersion: currentLatestVersion
                    })
                });

                const prepareData = await prepareResponse.json();

                if (prepareData.success) {
                    updateResult.innerHTML = `
                        <div class="alert alert-info">
                            ${document.getElementById('localizedUpdatePrepared')?.value || 'Update prepared. Restarting application...'}<br>
                            <small>${document.getElementById('localizedAppWillRestart')?.value || 'The application will close and restart with the new version.'}</small>
                        </div>
                    `;

                    setTimeout(async () => {
                        await fetch('/api/update/restart', {
                            method: 'POST',
                            headers: {
                                'Content-Type': 'application/json',
                            },
                            body: JSON.stringify({ tempPath: currentTempPath })
                        });

                        setTimeout(() => {
                            window.close();
                        }, 2000);
                    }, 2000);
                } else {
                    applyUpdateButton.disabled = false;
                    applyUpdateButton.textContent = document.getElementById('localizedApplyUpdateButton')?.value || 'Apply Update & Restart';
                    updateResult.innerHTML = `
                        <div class="alert alert-danger">
                            ${document.getElementById('localizedApplyFailed')?.value || 'Apply failed'}: ${prepareData.message}
                        </div>
                    `;
                }
            } catch (error) {
                console.error('Apply failed:', error);
                applyUpdateButton.disabled = false;
                applyUpdateButton.textContent = document.getElementById('localizedApplyUpdateButton')?.value || 'Apply Update & Restart';
                updateResult.innerHTML = `
                    <div class="alert alert-danger">
                        ${document.getElementById('localizedApplyFailed')?.value || 'Apply failed'}: ${error.message}
                    </div>
                `;
            }
        });
    }

    // 获取本地化字符串的辅助函数
    function getLocalizedString(key, defaultValue) {
        // 从页面元素获取本地化字符串
        const localizedElement = document.querySelector(`[data-localized="${key}"]`);
        if (localizedElement) {
            return localizedElement.textContent || localizedElement.value || defaultValue;
        }
        return defaultValue;
    }

    // 通知权限按钮初始化
    const requestPermissionButton = document.getElementById('requestPermissionButton');
    const enableNotificationsCheckbox = document.getElementById('enableNotifications');
    
    if (enableNotificationsCheckbox && enableNotificationsCheckbox.checked) {
        checkNotificationPermissionStatus();
    } else if (requestPermissionButton) {
        requestPermissionButton.disabled = true;
    }
    
    if (requestPermissionButton) {
        requestPermissionButton.addEventListener('click', async function() {
            if (!('Notification' in window)) {
                ModalUtils.alert(document.getElementById('localizedNotificationNotSupported')?.value || 'Notifications are not supported in this browser');
                return;
            }
            
            // Check if permission was previously blocked
            if (Notification.permission === 'denied') {
                const localizedBlockedMessage = document.getElementById('localizedNotificationBlockedMessage')?.value || 
                    '通知权限已被浏览器阻止。\n\n要重新启用通知权限，请按照以下步骤操作：\n\n1. 点击地址栏左侧的锁图标或信息图标\n2. 找到"通知"设置\n3. 将其设置为"允许"\n4. 刷新页面后再次尝试';
                ModalUtils.alert(localizedBlockedMessage);
                return;
            }
            
            try {
                const permission = await Notification.requestPermission();
                checkNotificationPermissionStatus();
                
                const localizedSuccess = document.getElementById('localizedNotificationRequestSuccess')?.value || 'Notification permission granted successfully';
                const localizedDenied = document.getElementById('localizedNotificationRequestDenied')?.value || 'Notification permission denied';
                const localizedDefault = document.getElementById('localizedNotificationPermissionDefault')?.value || 'Permission not granted';
                const localizedBlockedMessage = document.getElementById('localizedNotificationBlockedMessage')?.value || 
                    '通知权限已被浏览器阻止。\n\n要重新启用通知权限，请按照以下步骤操作：\n\n1. 点击地址栏左侧的锁图标或信息图标\n2. 找到"通知"设置\n3. 将其设置为"允许"\n4. 刷新页面后再次尝试';
                
                if (permission === 'granted') {
                    ModalUtils.alert(localizedSuccess);
                } else if (permission === 'denied') {
                    ModalUtils.alert(localizedDenied + '\n\n' + localizedBlockedMessage);
                } else {
                    ModalUtils.alert(localizedDefault);
                }
            } catch (error) {
                console.error('Error requesting notification permission:', error);
                
                // Check if error indicates permission was blocked
                if (error.message && error.message.includes('blocked')) {
                    const localizedBlockedMessage = document.getElementById('localizedNotificationBlockedMessage')?.value || 
                        '通知权限已被浏览器阻止。\n\n要重新启用通知权限，请按照以下步骤操作：\n\n1. 点击地址栏左侧的锁图标或信息图标\n2. 找到"通知"设置\n3. 将其设置为"允许"\n4. 刷新页面后再次尝试';
                    ModalUtils.alert(localizedBlockedMessage);
                } else {
                    ModalUtils.alert((document.getElementById('localizedNotificationRequestFailed')?.value || 'Failed to request notification permission') + ': ' + error.message);
                }
            }
        });
    }
    
    // 日期时间设置初始化
    const showSecondsHidden = document.getElementById('showSecondsHidden');
    const use12HourHidden = document.getElementById('use12HourHidden');
    const dateFormatHidden = document.getElementById('dateFormatHidden');
    const showSecondsCheckbox = document.getElementById('showSeconds');
    const use12HourCheckbox = document.getElementById('use12Hour');
    const dateFormatSelect = document.getElementById('dateFormat');
    
    if (showSecondsHidden && showSecondsCheckbox) {
        showSecondsCheckbox.checked = showSecondsHidden.value === 'true';
    }
    if (use12HourHidden && use12HourCheckbox) {
        use12HourCheckbox.checked = use12HourHidden.value === 'true';
    }
    if (dateFormatHidden && dateFormatSelect) {
        dateFormatSelect.value = dateFormatHidden.value;
    }
    
    // BepInEx 重置按钮事件监听器
    const resetBepInExButton = document.getElementById('resetBepInExSettings');
    if (resetBepInExButton) {
        resetBepInExButton.addEventListener('click', function() {
            window.resetBepInExSettings();
        });
    }
    
    // 处理查询参数中的成功/错误消息
    const urlParams = new URLSearchParams(window.location.search);
    const successMessage = urlParams.get('success');
    const errorMessage = urlParams.get('error');
    
    if (successMessage) {
        const successToast = document.getElementById('successToast');
        if (successToast) {
            const successToastBody = successToast.querySelector('.toast-body');
            if (successToastBody) {
                successToastBody.textContent = decodeURIComponent(successMessage);
            }
            successToast.classList.add('show');
            setTimeout(() => successToast.classList.remove('show'), 5000);
        }
        // 清除 URL 中的 success 参数
        window.history.replaceState({}, document.title, window.location.pathname);
    }
    
    if (errorMessage) {
        const errorToast = document.getElementById('errorToast');
        if (errorToast) {
            const errorToastBody = errorToast.querySelector('.toast-body');
            if (errorToastBody) {
                errorToastBody.textContent = decodeURIComponent(errorMessage);
            }
            errorToast.classList.add('show');
            setTimeout(() => errorToast.classList.remove('show'), 5000);
        }
        // 清除 URL 中的 error 参数
        window.history.replaceState({}, document.title, window.location.pathname);
    }
});

// 游戏启动模式选择相关功能 - 全局函数
function toggleLauncherPath(element) {
    const launcherPathSection = document.getElementById('launcherPathSection');
    const isExternal = element.value === 'external_program';
    launcherPathSection.style.display = isExternal ? 'block' : 'none';
}

// 更新频率显示/隐藏相关功能 - 全局函数
function toggleUpdateFrequency(element) {
    const updateFrequencySection = document.getElementById('updateFrequencySection');
    const isChecked = element.checked;
    updateFrequencySection.style.display = isChecked ? 'block' : 'none';
}

// 验证可执行文件 - 全局函数
function validateExe(input) {
    const errorElement = document.getElementById('launcherPathError');
    const launcherPath = document.getElementById('launcherPath');
    
    if (input.files.length > 0) {
        try {
            const file = input.files[0];
            const fileName = file.name.toLowerCase();
            
            // 使用更精确的匹配方式检查是否是Mod Manager自己的可执行文件
            const isModManagerExe = fileName === 'thmi mod manager.exe' || fileName === 'thmi_mod_manager.exe';
            
            if (isModManagerExe) {
                const localizedProhibitionText = document.getElementById('localizedProhibitionText')?.value || 'Cannot select the Mod Manager\'s own executable file.';
                errorElement.textContent = localizedProhibitionText;
                input.value = '';
                launcherPath.value = '';
            } else {
                errorElement.textContent = '';
                // 使用file.path或构建完整路径
                const filePath = file.path || '';
                launcherPath.value = filePath;
            }
        } catch (error) {
            // 如果发生错误，允许选择文件
            console.error('验证可执行文件时出错:', error);
            errorElement.textContent = '';
            launcherPath.value = input.files[0].name;
        }
    }
}



// 打开文件浏览器
function openFileBrowser(type) {
    // 确保window.fileBrowser对象存在
    if (typeof window.fileBrowser !== 'undefined' && window.fileBrowser !== null) {
        currentFileBrowserType = type;
        
        // 根据类型设置相应的回调函数
        if (type === 'executable') {
            window.fileBrowser.setOnFileSelected(function(filePath) {
                const launcherPath = document.getElementById('launcherPath');
                const errorElement = document.getElementById('launcherPathError');
                
                // 检查是否是Mod Manager自己的可执行文件
                const fileName = filePath.split('\\').pop().toLowerCase();
                const isModManagerExe = fileName === 'thmi mod manager.exe' || fileName === 'thmi_mod_manager.exe';
                
                if (isModManagerExe) {
                    const localizedProhibitionText = document.getElementById('localizedProhibitionText')?.value || 'Cannot select the Mod Manager\'s own executable file.';
                    errorElement.textContent = localizedProhibitionText;
                    if (launcherPath) launcherPath.value = '';
                } else {
                    errorElement.textContent = '';
                    if (launcherPath) launcherPath.value = filePath;
                }
                
                // 重置回调，避免内存泄漏
                window.fileBrowser.onFileSelected = null;
            });
            
            // 打开文件浏览器，设置标题和文件过滤器
            window.fileBrowser.open('file', {
                title: 'Select Executable File',
                fileFilter: 'launcher', // 使用启动器过滤器
                extensions: ['.exe', '.bat', '.cmd', '.ps1'], // 支持指定的启动器格式
                filterOptions: [
                    { value: 'launcher', label: 'Launcher Files' },
                    { value: 'exe', label: 'EXE Files' },
                    { value: 'bat', label: 'Batch Files' },
                    { value: 'cmd', label: 'CMD Files' },
                    { value: 'ps1', label: 'PowerShell Scripts' }
                ]
            });
        }
    } else {
        console.error('File browser is not available');
        ModalUtils.alert('错误', 'File browser is not available. Please refresh page and try again.');
    }
}



// 关闭文件浏览器
function closeFileBrowser() {
    const fileBrowserModal = document.getElementById('fileBrowserModal');
    fileBrowserModal.classList.remove('show');
    fileBrowserModal.style.display = 'none';
}

// 通知权限相关函数
function toggleNotificationPermission(checkbox) {
    const permissionSection = document.getElementById('notificationPermissionSection');
    const requestPermissionButton = document.getElementById('requestPermissionButton');
    
    if (checkbox.checked) {
        if ('Notification' in window) {
            checkNotificationPermissionStatus();
        } else {
            checkbox.checked = false;
            ModalUtils.alert(document.getElementById('localizedNotificationNotSupported')?.value || 'Notifications are not supported in this browser');
        }
    } else {
        if (requestPermissionButton) requestPermissionButton.disabled = true;
    }
}

function checkNotificationPermissionStatus() {
    const permissionStatus = document.getElementById('permissionStatusText');
    const requestPermissionButton = document.getElementById('requestPermissionButton');
    
    if (!('Notification' in window)) {
        permissionStatus.textContent = document.getElementById('localizedNotificationNotSupported')?.value || 'Notifications are not supported in this browser';
        permissionStatus.className = 'oojs-text-danger';
        if (requestPermissionButton) requestPermissionButton.disabled = true;
        return;
    }
    
    const permission = Notification.permission;
    const localizedGranted = document.getElementById('localizedNotificationPermissionGranted')?.value || 'Permission granted';
    const localizedDenied = document.getElementById('localizedNotificationPermissionDenied')?.value || 'Permission denied';
    const localizedDefault = document.getElementById('localizedNotificationPermissionDefault')?.value || 'Permission not granted';
    const localizedBlocked = document.getElementById('localizedNotificationPermissionBlocked')?.value || 'Permission blocked (click for instructions)';
    
    switch (permission) {
        case 'granted':
            permissionStatus.textContent = localizedGranted;
            permissionStatus.className = 'oojs-text-muted';
            if (requestPermissionButton) {
                requestPermissionButton.disabled = true;
                requestPermissionButton.textContent = document.getElementById('localizedNotificationPermissionGranted')?.value || '授权已获取';
            }
            break;
        case 'denied':
            permissionStatus.textContent = localizedDenied;
            permissionStatus.className = 'oojs-text-danger';
            if (requestPermissionButton) {
                requestPermissionButton.disabled = true;
                requestPermissionButton.textContent = document.getElementById('localizedNotificationPermissionDenied')?.value || '授权被拒绝';
            }
            break;
        case 'default':
            permissionStatus.textContent = localizedDefault;
            permissionStatus.className = 'oojs-text-muted';
            if (requestPermissionButton) {
                requestPermissionButton.disabled = false;
                requestPermissionButton.textContent = document.getElementById('localizedNotificationRequestPermission')?.value || 'Request Permission';
            }
            break;
        default:
            permissionStatus.textContent = permission;
            permissionStatus.className = 'oojs-text-muted';
    }
}

function saveDateTimeSettings() {
    const showSeconds = document.getElementById('showSeconds')?.checked || false;
    const use12Hour = document.getElementById('use12Hour')?.checked || false;
    const dateFormat = document.getElementById('dateFormat')?.value || 'yyyy-mm-dd';
    
    localStorage.setItem('datetimeShowSeconds', showSeconds);
    localStorage.setItem('datetimeUse12Hour', use12Hour);
    localStorage.setItem('datetimeDateFormat', dateFormat);
    
    const showSecondsHidden = document.getElementById('showSecondsHidden');
    const use12HourHidden = document.getElementById('use12HourHidden');
    const dateFormatHidden = document.getElementById('dateFormatHidden');
    
    if (showSecondsHidden) showSecondsHidden.value = showSeconds;
    if (use12HourHidden) use12HourHidden.value = use12Hour;
    if (dateFormatHidden) dateFormatHidden.value = dateFormat;
    
    if (window.dateTimeSettings) {
        window.dateTimeSettings.showSeconds = showSeconds;
        window.dateTimeSettings.use12Hour = use12Hour;
        window.dateTimeSettings.dateFormat = dateFormat;
    }
}

// 保存所有设置 - 全局函数
window.saveAllSettings = function() {
    const saveButton = document.getElementById('saveButton');
    if (!saveButton) return;
    
    saveButton.disabled = true;
    saveButton.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Saving...';
    
    const formData = new FormData();
    
    // 添加防跨站请求伪造令牌
    const token = document.querySelector('input[name="__RequestVerificationToken"]');
    if (token) {
        formData.append('__RequestVerificationToken', token.value);
    }
    
    // 添加语言设置
    const languageSelect = document.getElementById('languageSelect');
    if (languageSelect) {
        formData.append('language', languageSelect.value);
    }
    
    // 添加图标字体设置
    const iconFontHidden = document.getElementById('iconFontHidden');
    if (iconFontHidden) {
        formData.append('iconFont', iconFontHidden.value);
    }
    
    // 添加主题颜色
    const themeColorInput = document.querySelector('input[name="themeColor"]');
    if (themeColorInput) {
        formData.append('themeColor', themeColorInput.value);
    }
    
    // 添加启动模式
    const launchModeInput = document.querySelector('input[name="launchMode"]:checked');
    if (launchModeInput) {
        formData.append('launchMode', launchModeInput.value);
    }
    
    // 添加启动器路径
    const launcherPathInput = document.getElementById('launcherPath');
    if (launcherPathInput) {
        formData.append('launcherPath', launcherPathInput.value);
    }
    
    // 添加 Mods 路径
    const modsPathInput = document.getElementById('modsPath');
    if (modsPathInput) {
        formData.append('modsPath', modsPathInput.value);
    }
    
    // 添加游戏路径
    const gamePathInput = document.getElementById('gamePath');
    if (gamePathInput) {
        formData.append('gamePath', gamePathInput.value);
    }
    
    // 添加修改标题
    const modifyTitleInput = document.getElementById('modifyTitle');
    if (modifyTitleInput) {
        formData.append('modifyTitle', modifyTitleInput.checked);
    }
    
    // 添加自动检查更新
    const autoCheckUpdatesInput = document.getElementById('autoCheckUpdates');
    if (autoCheckUpdatesInput) {
        formData.append('autoCheckUpdates', autoCheckUpdatesInput.checked);
    }
    
    // 添加更新频率
    const updateFrequencySelect = document.getElementById('updateFrequency');
    if (updateFrequencySelect) {
        formData.append('updateFrequency', updateFrequencySelect.value);
    }
    
    // 添加启用通知
    const enableNotificationsInput = document.getElementById('enableNotifications');
    if (enableNotificationsInput) {
        formData.append('enableNotifications', enableNotificationsInput.checked);
    }
    
    // 添加显示秒数
    const showSecondsHidden = document.getElementById('showSecondsHidden');
    if (showSecondsHidden) {
        formData.append('showSeconds', showSecondsHidden.value);
    }
    
    // 添加使用12小时制
    const use12HourHidden = document.getElementById('use12HourHidden');
    if (use12HourHidden) {
        formData.append('use12Hour', use12HourHidden.value);
    }
    
    // 添加日期格式
    const dateFormatHidden = document.getElementById('dateFormatHidden');
    if (dateFormatHidden) {
        formData.append('dateFormat', dateFormatHidden.value);
    }
    
    // 添加分隔符
    const dateSeparatorSelect = document.getElementById('dateSeparator');
    if (dateSeparatorSelect) {
        formData.append('dateSeparator', dateSeparatorSelect.value);
    }
    
    fetch('/Settings?handler=SaveLanguage', {
        method: 'POST',
        body: formData,
        headers: {
            'RequestVerificationToken': token?.value || ''
        }
    })
    .then(response => {
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
        return response.text();
    })
    .then(html => {
        saveButton.disabled = false;
        saveButton.innerHTML = '<span style="font-family: \'Segoe Fluent Icons\'; margin-right: 4px;">&#xE74E;</span> Save Changes';
        
        // 显示成功提示
        const successToast = document.getElementById('successToast');
        if (successToast) {
            successToast.classList.add('show');
            setTimeout(() => successToast.classList.remove('show'), 3000);
        }
        
        // 刷新页面以显示新设置
        setTimeout(() => {
            window.location.reload();
        }, 1000);
    })
    .catch(error => {
        console.error('保存设置失败:', error);
        saveButton.disabled = false;
        saveButton.innerHTML = '<span style="font-family: \'Segoe Fluent Icons\'; margin-right: 4px;">&#xE74E;</span> Save Changes';
        ModalUtils.alert('保存失败', '保存设置失败，请重试: ' + error.message);
    });
}
