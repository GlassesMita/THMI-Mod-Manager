// 指针选择器功能
class CursorSelector {
    constructor() {
        this.currentCursor = 'default';
        this.radioOptions = document.querySelectorAll('.cursor-radio-option input[type="radio"]');
        this.osuCursorInstance = null;
        
        this.init();
    }

    init() {
        this.bindEvents();
        this.loadSavedCursor();
    }

    bindEvents() {
        // 绑定单选框事件
        this.radioOptions.forEach(radio => {
            radio.addEventListener('change', (e) => {
                this.handleCursorChange(e.target.value);
            });
        });
    }

    handleCursorChange(cursorType) {
        this.currentCursor = cursorType;
        this.applyCursor(cursorType);
        this.saveCursorPreference(cursorType);
        
        // 触发自定义事件
        window.dispatchEvent(new CustomEvent('cursorChanged', { 
            detail: { cursorType: cursorType } 
        }));
    }

    applyCursor(cursorType) {
        // 移除所有光标类
        document.body.classList.remove('mystia-cursor', 'osu-cursor');
        
        // 移除之前添加的光标样式
        const existingCursorStyle = document.getElementById('dynamic-cursor-style');
        if (existingCursorStyle) {
            existingCursorStyle.remove();
        }
        
        // 卸载osu光标（如果当前正在使用）
        if (this.currentCursor === 'osu') {
            this.unloadOsuCursor();
        }
        
        // 应用新的光标样式
        switch (cursorType) {
            case 'mystia':
                // Mystia Cursor通过CSS样式加载
                document.body.classList.add('mystia-cursor');
                break;
            case 'osu':
                // osu! lazer指针通过JavaScript文件加载
                this.loadOsuCursor();
                break;
            default:
                // System Default为不加载任何样式或CSS
                break;
        }
        
        this.currentCursor = cursorType;
    }



    loadOsuCursor() {
        // 动态加载osu-cursor.js文件（非模块方式）
        if (typeof osuCursor !== 'undefined') {
            // 如果已经加载过，直接使用
            if (this.osuCursorInstance) {
                // 重新初始化
                this.osuCursorInstance.stop();
            }
            this.osuCursorInstance = new osuCursor();
        } else {
            // 动态加载osu-cursor.js（确保不以模块方式加载）
            const script = document.createElement('script');
            script.src = '/src/osu-cursor.js';
            // 移除type="module"属性，确保以传统方式加载
            script.onload = () => {
                // 等待下一个事件循环确保osuCursor已定义
                setTimeout(() => {
                    if (typeof osuCursor !== 'undefined') {
                        this.osuCursorInstance = new osuCursor();
                    } else {
                        console.error('osuCursor未正确定义');
                    }
                }, 0);
            };
            script.onerror = (error) => {
                console.error('加载osu-cursor.js失败:', error);
            };
            document.head.appendChild(script);
        }
    }

    unloadOsuCursor() {
        // 卸载osu光标
        if (this.osuCursorInstance) {
            this.osuCursorInstance.stop();
            this.osuCursorInstance = null;
        }
    }

    saveCursorPreference(cursorType) {
        // 保存到 localStorage
        try {
            localStorage.setItem('preferredCursor', cursorType);
        } catch (e) {
            console.warn('无法保存光标偏好设置:', e);
        }

        // 发送到服务器保存（如果可用）
        if (window.saveCursorSetting) {
            window.saveCursorSetting(cursorType);
        }
    }

    loadSavedCursor() {
        // 从 localStorage 加载保存的光标
        try {
            const savedCursor = localStorage.getItem('preferredCursor');
            if (savedCursor) {
                this.setCursorOption(savedCursor);
                this.applyCursor(savedCursor);
            }
        } catch (e) {
            console.warn('无法加载保存的光标设置:', e);
        }
    }

    setCursorOption(cursorType) {
        // 设置对应的单选框
        const targetRadio = document.querySelector(`input[type="radio"][value="${cursorType}"]`);
        if (targetRadio) {
            targetRadio.checked = true;
            this.currentCursor = cursorType;
        }
    }

    // 公共 API
    getCurrentCursor() {
        return this.currentCursor;
    }

    setCursor(cursorType) {
        this.setCursorOption(cursorType);
        this.handleCursorChange(cursorType);
    }

    // 静态方法：创建全局实例
    static init() {
        if (!window.cursorSelector) {
            window.cursorSelector = new CursorSelector();
        }
        return window.cursorSelector;
    }
}

// 初始化光标选择器
document.addEventListener('DOMContentLoaded', () => {
    CursorSelector.init();
});

// 添加 CSS 样式到页面
function addCursorStyles() {
    // 获取主题色，默认为#c670ff
    const themeColor = getComputedStyle(document.documentElement)
        .getPropertyValue('--theme-color').trim() || '#c670ff';
    
    // 替换SVG中的颜色值
    const svgContent = `
        <svg xmlns='http://www.w3.org/2000/svg' width='32' height='32' viewBox='0 0 32 32'>
            <circle cx='16' cy='16' r='14' fill='${themeColor}' stroke='%23ffffff' stroke-width='2'/>
            <circle cx='16' cy='16' r='6' fill='%23ffffff'/>
        </svg>
    `.replace(/\s+/g, ' ').trim();
    
    const encodedSvg = encodeURIComponent(svgContent);
    
    const style = document.createElement('style');
    style.textContent = `
        /* 全局光标样式 */
        body.mystia-cursor {
            cursor: url('/Resources/Cursor.cur'), auto !important;
        }
        
        body.mystia-cursor * {
            cursor: url('/Resources/Cursor.cur'), auto !important;
        }
        
        body.osu-cursor {
            cursor: url("data:image/svg+xml,${encodedSvg}") 16 16, auto !important;
        }
        
        body.osu-cursor * {
            cursor: url("data:image/svg+xml,${encodedSvg}") 16 16, auto !important;
        }
        
        /* 确保链接和按钮保持指针样式 */
        body.mystia-cursor a,
        body.mystia-cursor button,
        body.mystia-cursor [role="button"],
        body.mystia-cursor input[type="submit"],
        body.mystia-cursor input[type="button"] {
            cursor: url('/Resources/Cursor.cur'), pointer !important;
        }
        
        body.osu-cursor a,
        body.osu-cursor button,
        body.osu-cursor [role="button"],
        body.osu-cursor input[type="submit"],
        body.osu-cursor input[type="button"] {
            cursor: url("data:image/svg+xml,${encodedSvg}") 16 16, pointer !important;
        }
    `;
    document.head.appendChild(style);
}

// 添加样式到页面
addCursorStyles();

// 导出供其他模块使用
if (typeof module !== 'undefined' && module.exports) {
    module.exports = CursorSelector;
}