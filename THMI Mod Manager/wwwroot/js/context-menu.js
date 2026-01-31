class ContextMenu {
    constructor(options = {}) {
        this.menuElement = null;
        this.backdropElement = null;
        this.items = [];
        this.highlightedIndex = -1;
        this.isVisible = false;
        this.onItemClick = options.onItemClick || (() => {});
        this.menuId = options.menuId || 'cdx-context-menu';
        this.ignoredElements = options.ignoredElements || ['input', 'textarea', 'select'];
        this.init();
    }

    init() {
        this.createMenuElement();
        this.createBackdropElement();
        this.bindEvents();
    }

    createMenuElement() {
        this.menuElement = document.createElement('div');
        this.menuElement.id = this.menuId;
        this.menuElement.className = 'cdx-context-menu';
        document.body.appendChild(this.menuElement);
    }

    createBackdropElement() {
        this.backdropElement = document.createElement('div');
        this.backdropElement.id = this.menuId + '-backdrop';
        this.backdropElement.className = 'cdx-context-menu-backdrop';
        document.body.appendChild(this.backdropElement);
    }

    setMenuItems(items) {
        this.items = items;
        this.render();
        
        if (this.items.length === 0) {
            this.menuElement.style.display = 'none';
        } else {
            this.menuElement.style.display = '';
        }
    }

    enableDefaultMenu() {
        const defaultMenuItems = [
            { type: 'header', label: 'Navigation' },
            { icon: 'icon-home', label: 'Home', action: 'home', shortcut: 'Alt+1' },
            { icon: 'icon-settings', label: 'Settings', action: 'settings', shortcut: 'Alt+S' },
            { icon: 'icon-about', label: 'About', action: 'about', shortcut: 'Alt+A' },
            { type: 'separator' },
            { type: 'header', label: 'Page' },
            { icon: 'icon-reload', label: 'Reload', action: 'reload', shortcut: 'F5' },
            { icon: 'icon-back', label: 'Back', action: 'back', shortcut: 'Alt+←' },
            { icon: 'icon-forward', label: 'Forward', action: 'forward', shortcut: 'Alt+→' },
            { type: 'separator' },
            { icon: 'icon-devtools', label: 'Developer Tools', action: 'devtools', shortcut: 'F12' },
        ];
        this.setMenuItems(defaultMenuItems);
    }

    disable() {
        this.items = [];
        this.hide();
        this.menuElement.style.display = 'none';
    }

    render() {
        if (!this.menuElement) return;

        this.menuElement.innerHTML = '';

        this.items.forEach((item, index) => {
            if (item.type === 'separator') {
                const separator = document.createElement('div');
                separator.className = 'cdx-context-menu__separator';
                this.menuElement.appendChild(separator);
            } else if (item.type === 'header') {
                const header = document.createElement('div');
                header.className = 'cdx-context-menu__header';
                header.textContent = item.label;
                this.menuElement.appendChild(header);
            } else {
                const menuItem = document.createElement('div');
                menuItem.className = 'cdx-context-menu__item';
                menuItem.dataset.index = index;

                if (item.disabled) {
                    menuItem.classList.add('cdx-context-menu__item--disabled');
                    menuItem.tabIndex = -1;
                }

                if (item.icon) {
                    const icon = document.createElement('span');
                    icon.className = 'cdx-context-menu__icon';
                    const iconContent = item.iconContent || this.getIconContent(item.icon);
                    icon.innerHTML = `<i data-icon="${item.icon}">${iconContent}</i>`;
                    menuItem.appendChild(icon);
                }

                if (!item.iconOnly && item.label) {
                    const label = document.createElement('span');
                    label.className = 'cdx-context-menu__label';
                    label.textContent = item.label;
                    menuItem.appendChild(label);
                }

                if (item.shortcut) {
                    const shortcut = document.createElement('span');
                    shortcut.className = 'cdx-context-menu__shortcut';
                    shortcut.textContent = item.shortcut;
                    menuItem.appendChild(shortcut);
                }

                this.menuElement.appendChild(menuItem);
            }
        });
    }

    getIconContent(iconName) {
        const icons = {
            'icon-refresh': '&#xE72C;',
            'icon-copy': '&#xE8C8;',
            'icon-paste': '&#xE77F;',
            'icon-cut': '&#xE8C6;',
            'icon-select-all': '&#xE8B3;',
            'icon-devtools': '&#xEBCE;',
            'icon-settings': '&#xE713;',
            'icon-home': '&#xE80F;',
            'icon-about': '&#xE70B;',
            'icon-back': '&#xE0A6;',
            'icon-forward': '&#xE0AB;',
            'icon-reload': '&#xE72C;',
            'icon-stop': '&#xE71A;',
            'icon-music': '&#xE8D6;',
            'icon-play': '&#xE768;',
            'icon-pause': '&#xE769;',
            'icon-pin': '&#xEC7A;',
        };
        return icons[iconName] || '';
    }

    bindEvents() {
        document.addEventListener('contextmenu', (e) => this.onContextMenu(e));
        document.addEventListener('click', (e) => this.onClick(e));
        document.addEventListener('keydown', (e) => this.onKeyDown(e));
        this.backdropElement.addEventListener('click', () => this.hide());

        this.menuElement.addEventListener('mouseover', (e) => {
            const item = e.target.closest('.cdx-context-menu__item');
            if (item && !item.classList.contains('cdx-context-menu__item--disabled')) {
                const index = parseInt(item.dataset.index, 10);
                this.highlightItem(index);
            }
        });

        this.menuElement.addEventListener('click', (e) => {
            const item = e.target.closest('.cdx-context-menu__item');
            if (item && !item.classList.contains('cdx-context-menu__item--disabled')) {
                const index = parseInt(item.dataset.index, 10);
                this.selectItem(index);
            }
        });
    }

    onContextMenu(e) {
        if (this.isIgnoredElement(e.target)) {
            return;
        }

        e.preventDefault();
        this.show(e.clientX, e.clientY);
    }

    onClick(e) {
        if (this.isVisible && !this.menuElement.contains(e.target) && !e.target.closest('.cdx-context-menu-backdrop')) {
            this.hide();
        }
    }

    onKeyDown(e) {
        if (!this.isVisible) return;

        switch (e.key) {
            case 'Escape':
                this.hide();
                e.preventDefault();
                break;
            case 'ArrowUp':
                this.navigate(-1);
                e.preventDefault();
                break;
            case 'ArrowDown':
                this.navigate(1);
                e.preventDefault();
                break;
            case 'Enter':
                if (this.highlightedIndex >= 0) {
                    this.selectItem(this.highlightedIndex);
                }
                e.preventDefault();
                break;
        }
    }

    isIgnoredElement(element) {
        for (const selector of this.ignoredElements) {
            if (element.matches(selector) || element.closest(selector)) {
                return true;
            }
        }
        return false;
    }

    show(x, y) {
        if (this.items.length === 0) {
            return;
        }

        const menuWidth = this.menuElement.offsetWidth || 200;
        const menuHeight = this.menuElement.offsetHeight || 0;
        const windowWidth = window.innerWidth;
        const windowHeight = window.innerHeight;

        let adjustedX = x;
        let adjustedY = y;

        if (x + menuWidth > windowWidth - 10) {
            adjustedX = x - menuWidth - 10;
        }

        if (y + menuHeight > windowHeight - 10) {
            adjustedY = y - menuHeight - 10;
        }

        if (adjustedX < 10) adjustedX = 10;
        if (adjustedY < 10) adjustedY = 10;

        this.menuElement.style.left = adjustedX + 'px';
        this.menuElement.style.top = adjustedY + 'px';

        this.menuElement.classList.add('cdx-context-menu--visible');
        this.backdropElement.classList.add('cdx-context-menu-backdrop--visible');

        this.highlightedIndex = -1;
        this.isVisible = true;
    }

    hide() {
        this.menuElement.classList.remove('cdx-context-menu--visible');
        this.backdropElement.classList.remove('cdx-context-menu-backdrop--visible');
        this.isVisible = false;
        this.highlightedIndex = -1;
    }

    navigate(direction) {
        const enabledItems = this.items
            .map((item, index) => ({ item, index }))
            .filter(({ item }) => item.type !== 'separator' && item.type !== 'header' && !item.disabled);

        if (enabledItems.length === 0) return;

        let newIndex = enabledItems.findIndex(({ index }) => index === this.highlightedIndex);
        if (newIndex === -1) {
            newIndex = direction > 0 ? 0 : enabledItems.length - 1;
        } else {
            newIndex += direction;
            if (newIndex < 0) newIndex = enabledItems.length - 1;
            if (newIndex >= enabledItems.length) newIndex = 0;
        }

        this.highlightItem(enabledItems[newIndex].index);
    }

    highlightItem(index) {
        const items = this.menuElement.querySelectorAll('.cdx-context-menu__item');
        items.forEach((item, i) => {
            if (i === index) {
                item.classList.add('cdx-context-menu__item--highlighted');
                item.focus();
            } else {
                item.classList.remove('cdx-context-menu__item--highlighted');
            }
        });
        this.highlightedIndex = index;
    }

    selectItem(index) {
        const item = this.items[index];
        if (item && item.type !== 'separator' && item.type !== 'header' && !item.disabled) {
            this.hide();
            this.onItemClick(item, index);
        }
    }

    destroy() {
        if (this.menuElement) {
            this.menuElement.remove();
        }
        if (this.backdropElement) {
            this.backdropElement.remove();
        }
    }
}

const contextMenu = new ContextMenu({
    menuId: 'custom-context-menu',
    ignoredElements: ['input', 'textarea', 'select', '[contenteditable="true"]'],
    onItemClick: (item) => {
        console.log('[ContextMenu] Clicked:', item.label);

        const actionType = item.type || 'action';

        switch (actionType) {
            case 'url':
                if (item.url) {
                    const target = item.target || '_self';
                    if (target === '_blank') {
                        window.open(item.url, '_blank');
                    } else {
                        window.location.href = item.url;
                    }
                }
                break;

            case 'function':
                if (item.fn && typeof window[item.fn] === 'function') {
                    window[item.fn](item, item.data);
                } else if (item.fn && typeof item.fn === 'function') {
                    item.fn(item, item.data);
                }
                break;

            case 'callback':
                if (item.callback && typeof item.callback === 'function') {
                    item.callback(item, item.data);
                }
                break;

            case 'action':
            default:
                switch (item.action) {
                    case 'reload':
                        window.location.reload();
                        break;
                    case 'back':
                        if (window.history.length > 1) {
                            window.history.back();
                        }
                        break;
                    case 'forward':
                        if (window.history.length > 1) {
                            window.history.forward();
                        }
                        break;
                    case 'devtools':
                        if (typeof DevTools !== 'undefined') {
                            DevTools.toggle();
                        } else {
                            window.devtools.open();
                        }
                        break;
                    case 'settings':
                        window.location.href = '/Settings';
                        break;
                    case 'home':
                        window.location.href = '/';
                        break;
                    case 'about':
                        window.location.href = '/About';
                        break;
                    case 'internetSettings':
                        window.location.href = 'ms-settings:network-status';
                        break;
                }
                break;
        }
    }
});

contextMenu.menuElement.style.display = 'none';

window.ContextMenu = ContextMenu;
window.contextMenu = contextMenu;
