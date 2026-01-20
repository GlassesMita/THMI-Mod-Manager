document.addEventListener('DOMContentLoaded', function() {
    const form = document.getElementById('settingsForm');
    const successToast = document.getElementById('successToast');
    const selects = document.querySelectorAll('.form-select');
    let isSaving = false; // 防止重复提交
    
    // 为浏览按钮添加事件监听器
    const browseLauncherPath = document.getElementById('browseLauncherPath');
    if (browseLauncherPath) {
        browseLauncherPath.addEventListener('click', () => openFileBrowser('executable'));
    }
    
    // 为所有下拉框添加动画效果
    selects.forEach((select, index) => {
        // 改变值时的动画
        select.addEventListener('change', function() {
            // 添加成功反馈
            this.style.borderColor = '#198754';
            setTimeout(() => {
                this.style.borderColor = '';
            }, 1500);
        });
    });
    
    form.addEventListener('submit', function(e) {
        e.preventDefault(); // 阻止默认表单提交
        
        // 防止重复提交
        if (isSaving) {
            console.log('Settings already being saved, ignoring duplicate submission');
            return;
        }
        isSaving = true;
        
        // 添加保存动画
        const submitButton = form.querySelector('.btn-primary');
        const saveText = document.getElementById('saveText');
        const loadingSpinner = document.getElementById('loadingSpinner');
        
        submitButton.classList.add('loading');
        submitButton.disabled = true;
        saveText.style.display = 'none';
        loadingSpinner.style.display = 'inline-block';
        
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
            submitButton.classList.remove('loading');
            submitButton.disabled = false;
            saveText.style.display = 'inline';
            loadingSpinner.style.display = 'none';
            isSaving = false;
            
            // 显示成功提示
            successToast.classList.add('show');
            
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
            submitButton.classList.remove('loading');
            submitButton.disabled = false;
            saveText.style.display = 'inline';
            loadingSpinner.style.display = 'none';
            
            // 显示错误提示
            alert('保存设置失败，请重试: ' + error.message);
        });
    });
    
    // 为光标选项添加交互效果
    const cursorRadios = document.querySelectorAll('input[name="cursorType"]');
    cursorRadios.forEach(radio => {
        radio.addEventListener('change', function() {
            // 移除所有选项的高亮
            document.querySelectorAll('.cursor-radio-option').forEach(option => {
                option.style.backgroundColor = 'transparent';
            });
            
            // 为选中的选项添加高亮
            if (this.checked) {
                const themeColor = document.getElementById('colorPreviewText') ? document.getElementById('colorPreviewText').textContent : '#c670ff';
                this.closest('.cursor-radio-option').style.backgroundColor = 'rgba(' + parseInt(themeColor.substring(1, 3), 16) + ', ' + parseInt(themeColor.substring(3, 5), 16) + ', ' + parseInt(themeColor.substring(5, 7), 16) + ', 0.1)';
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
                    updateResult.innerHTML = `
                        <div class="alert alert-info">
                            <h6>${localizedUpdateAvailable.replace('{0}', data.latestVersion)}</h6>
                            <p>${data.releaseNotes || localizedNoReleaseNotes}</p>
                            <div class="mt-2">
                                <a href="${data.downloadUrl}" target="_blank" class="btn btn-primary btn-sm">
                                    ${localizedDownloadUpdate}
                                </a>
                            </div>
                        </div>
                    `;
                    
                    // Dispatch custom event for global update notification
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
            // 更新设置将通过表单提交保存
            console.log('Auto check updates setting changed to:', this.checked);
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
});

// 游戏启动模式选择相关功能 - 全局函数
function toggleLauncherPath(element) {
    const launcherPathSection = document.getElementById('launcherPathSection');
    const isExternal = element.value === 'external_program';
    launcherPathSection.style.display = isExternal ? 'block' : 'none';
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
        alert('File browser is not available. Please refresh the page and try again.');
    }
}



// 关闭文件浏览器
function closeFileBrowser() {
    const fileBrowserModal = document.getElementById('fileBrowserModal');
    fileBrowserModal.classList.remove('show');
    fileBrowserModal.style.display = 'none';
}
