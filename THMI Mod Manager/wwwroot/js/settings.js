document.addEventListener('DOMContentLoaded', function() {
    const form = document.getElementById('settingsForm');
    const successToast = document.getElementById('successToast');
    const selects = document.querySelectorAll('.form-select');
    
    // 为浏览按钮添加事件监听器
    const browseCustomLauncher = document.getElementById('browseCustomLauncher');
    if (browseCustomLauncher) {
        browseCustomLauncher.addEventListener('click', () => openFileBrowser('executable'));
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
        // 添加保存动画
        const submitButton = form.querySelector('.btn-primary');
        submitButton.classList.add('loading');
        submitButton.disabled = true;
        
        // 模拟表单提交过程
        setTimeout(function() {
            submitButton.classList.remove('loading');
            submitButton.disabled = false;
            
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
        }, 1000);
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

    // 从配置加载开发者设置
    function loadDeveloperSettings() {
        // 从AppConfig加载实际的设置值
        const isDevMode = document.getElementById('devMode') ? document.getElementById('devMode').checked : false;
        const showCVE = document.getElementById('showCVEWarning') ? document.getElementById('showCVEWarning').checked : false;
        
        // 这些值将由Razor在页面加载时设置
    }
    
    // 保存开发者设置
    window.saveDeveloperSettings = function() {
        const devMode = document.getElementById('devMode').checked;
        const showCVEWarning = document.getElementById('showCVEWarning').checked;
        
        // 创建表单数据
        const formData = new FormData();
        formData.append('devMode', devMode);
        formData.append('showCVEWarning', showCVEWarning);
        
        // 发送到后端保存
        fetch('/settings?handler=SaveDeveloperSettings', {
            method: 'POST',
            body: formData,
            headers: {
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
            }
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                // 保存到本地存储
                localStorage.setItem('developerMode', devMode);
                localStorage.setItem('showCVEWarning', showCVEWarning);
                
                // 如果开发模式开启，立即显示开发者设置
                if (devMode) {
                    const developerSection = document.getElementById('developerSection');
                    if (developerSection) developerSection.classList.add('show');
                }
                
                // 显示成功提示
                const successToast = document.getElementById('successToast');
                successToast.textContent = data.message || '开发者设置已保存！';
                successToast.classList.add('show');
                
                setTimeout(function() {
                    successToast.classList.remove('show');
                    successToast.textContent = '设置保存成功！';
                }, 3000);
            }
        })
        .catch(error => {
            console.error('保存开发者设置失败:', error);
            alert('保存失败，请重试');
        });
    };
    
    // 标题点击事件
    const settingsTitle = document.getElementById('settingsTitle');
    const clickCounter = document.getElementById('clickCounter');
    const clickCountSpan = document.getElementById('clickCount');
    const developerSection = document.getElementById('developerSection');
    
    if (settingsTitle && clickCounter && clickCountSpan && developerSection) {
        let clickCount = 0;
        
        settingsTitle.addEventListener('click', function() {
            clickCount++;
            clickCountSpan.textContent = clickCount;
            clickCounter.classList.add('show');
            
            if (clickCount >= 10) {
                developerSection.classList.add('show');
                clickCounter.style.display = 'none';
                loadDeveloperSettings();
                
                // 显示解锁提示
                const successToast = document.getElementById('successToast');
                successToast.textContent = '🔓 开发者选项已解锁！';
                successToast.classList.add('show');
                
                setTimeout(function() {
                    successToast.classList.remove('show');
                    successToast.textContent = '设置保存成功！';
                }, 3000);
            }
            
            // 3秒后隐藏计数器
            setTimeout(function() {
                if (clickCount < 10) {
                    clickCounter.classList.remove('show');
                }
            }, 3000);
        });
    }
    
    // 初始化开发者设置（如果已解锁）
    if (localStorage.getItem('developerMode') === 'true') {
        if (developerSection) developerSection.classList.add('show');
        loadDeveloperSettings();
    }

    // 初始化游戏版本设置
    const gameVersionLegitimate = document.getElementById('gameVersionLegitimate');
    const gameVersionPirated = document.getElementById('gameVersionPirated');
    const customLauncherSection = document.getElementById('customLauncherSection');
    
    // 确保正确的游戏版本被选中
    if (gameVersionLegitimate && gameVersionPirated) {
        // 这些值将由Razor在页面加载时设置
        const gameVersion = document.getElementById('gameVersionHidden') ? document.getElementById('gameVersionHidden').value : 'legitimate';
        if (gameVersion === 'legitimate') {
            gameVersionLegitimate.checked = true;
            customLauncherSection.style.display = 'none';
        } else {
            gameVersionPirated.checked = true;
            customLauncherSection.style.display = 'block';
        }
    }
});

// 游戏版本选择相关功能 - 全局函数
function toggleCustomLauncher(element) {
    const customLauncherSection = document.getElementById('customLauncherSection');
    const isPirated = element.value === 'pirated';
    customLauncherSection.style.display = isPirated ? 'block' : 'none';
}

// 验证可执行文件 - 全局函数
function validateExe(input) {
    const errorElement = document.getElementById('customLauncherError');
    const customLauncherPath = document.getElementById('customLauncherPath');
    
    if (input.files.length > 0) {
        try {
            const file = input.files[0];
            const fileName = file.name.toLowerCase();
            
            // 使用更精确的匹配方式检查是否是Mod Manager自己的可执行文件
            const isModManagerExe = fileName === 'thmi mod manager.exe' || fileName === 'thmi_mod_manager.exe';
            
            if (isModManagerExe) {
                errorElement.textContent = '@AppConfig.GetLocalized("Settings:ProhibitionText", "Cannot select the Mod Manager\'s own executable file.")';
                input.value = '';
                customLauncherPath.value = '';
            } else {
                errorElement.textContent = '';
                // 使用file.path或构建完整路径
                const filePath = file.path || '';
                customLauncherPath.value = filePath;
            }
        } catch (error) {
            // 如果发生错误，允许选择文件
            console.error('验证可执行文件时出错:', error);
            errorElement.textContent = '';
            customLauncherPath.value = input.files[0].name;
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
                const customLauncherPath = document.getElementById('customLauncherPath');
                const errorElement = document.getElementById('customLauncherError');
                
                // 检查是否是Mod Manager自己的可执行文件
                const fileName = filePath.split('\\').pop().toLowerCase();
                const isModManagerExe = fileName === 'thmi mod manager.exe' || fileName === 'thmi_mod_manager.exe';
                
                if (isModManagerExe) {
                    errorElement.textContent = 'Cannot select the Mod Manager\'s own executable file.';
                    if (customLauncherPath) customLauncherPath.value = '';
                } else {
                    errorElement.textContent = '';
                    if (customLauncherPath) customLauncherPath.value = filePath;
                }
                
                // 重置回调，避免内存泄漏
                window.fileBrowser.onFileSelected = null;
            });
            
            // 打开文件浏览器，设置标题和隐藏文件过滤器
            window.fileBrowser.open('file', {
                title: 'Select Executable File',
                hideFileFilter: true // 隐藏文件过滤器
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
