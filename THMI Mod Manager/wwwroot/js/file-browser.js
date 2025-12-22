// file-browser.js - 可复用的文件浏览器组件

// 格式化文件大小的辅助函数
function formatFileSize(bytes) {
    if (bytes === 0) return '0 Bytes';
    
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

class FileBrowser {
    constructor() {
        this.currentDirectory = '';
        this.currentFileBrowserType = 'file'; // 默认为文件选择
        this.fileExtensions = []; // 允许的文件扩展名
        this.onFileSelected = null; // 文件选择回调函数
        this.onFolderSelected = null; // 文件夹选择回调函数
        this.currentFileFilter = 'all'; // 当前文件过滤类型
        this.driveData = []; // 存储驱动器数据
        
        // 绑定事件处理器的上下文
        this.onFileFilterChange = this.onFileFilterChange.bind(this);
        this.onDriveSelected = this.onDriveSelected.bind(this);
        this.navigateToAddress = this.navigateToAddress.bind(this);
    }

    // 初始化文件浏览器
    init() {
        // 加载驱动器列表
        this.loadDrives();
        // 加载应用运行目录的文件
        this.loadAppDirectory();
    }

    // 设置文件选择回调
    setOnFileSelected(callback) {
        this.onFileSelected = callback;
    }

    // 设置文件夹选择回调
    setOnFolderSelected(callback) {
        this.onFolderSelected = callback;
    }

    // 设置允许的文件扩展名
    setAllowedExtensions(extensions) {
        this.fileExtensions = extensions;
    }

    // 设置文件过滤类型
    setFileFilter(filterType) {
        this.currentFileFilter = filterType;
        // 如果当前正在浏览目录，重新加载文件
        if (this.currentDirectory) {
            this.loadDirectoryFiles(this.currentDirectory);
        }
    }

    // 打开文件浏览器
    open(type, options = {}) {
        this.currentFileBrowserType = type;
        const fileBrowserModal = document.getElementById('fileBrowserModal');
        const fileFilterSelect = document.getElementById('fileFilterSelect');
        
        // 设置选项
        if (options.title) {
            this.setTitle(options.title);
        } else {
            // 使用默认标题
            this.setTitle(this.getDefaultTitle(type));
        }
        
        if (options.extensions) {
            this.setAllowedExtensions(options.extensions);
        }
        
        if (options.onFileSelected) {
            this.setOnFileSelected(options.onFileSelected);
        }
        
        if (options.onFolderSelected) {
            this.setOnFolderSelected(options.onFolderSelected);
        }
        
        // 重置文件过滤器为'all'
        this.currentFileFilter = 'all';
        if (fileFilterSelect) {
            fileFilterSelect.value = 'all';
        }
        
        // 确保模态框初始状态正确
        fileBrowserModal.style.display = 'block';
        
        // 强制重排以确保初始状态生效
        fileBrowserModal.offsetHeight;
        
        // 添加显示类以触发动画
        fileBrowserModal.classList.add('show');
        
        // 根据文件类型显示或隐藏文件过滤选择器
        // 如果options.hideFileFilter为true，则始终隐藏文件过滤器
        if (type === 'file' && fileFilterSelect && !options.hideFileFilter) {
            fileFilterSelect.style.display = 'block';
        } else {
            if (fileFilterSelect) {
                fileFilterSelect.style.display = 'none';
            }
        }
        
        // 加载应用运行目录
        this.loadAppDirectory();
    }

    // 关闭文件浏览器
    close() {
        const fileBrowserModal = document.getElementById('fileBrowserModal');
        
        if (fileBrowserModal) {
            // 移除显示类以触发动画
            fileBrowserModal.classList.remove('show');
            
            // 添加延迟以确保关闭动画能够正确播放
            setTimeout(() => {
                fileBrowserModal.style.display = 'none';
                // 重置滚动位置，防止下次打开时位置异常
                const fileList = document.getElementById('filesList');
                if (fileList) {
                    fileList.scrollTop = 0;
                }
                
                // 重置回调函数，避免内存泄漏
                this.onFileSelected = null;
                this.onFolderSelected = null;
            }, 300); // 与CSS过渡时间保持一致
        }
    }

    // 设置文件浏览器标题
    setTitle(title) {
        const fileBrowserTitle = document.querySelector('.file-browser-title');
        if (fileBrowserTitle) {
            fileBrowserTitle.textContent = title;
        }
    }

    // 获取默认标题
    getDefaultTitle(type) {
        let title = 'Select File';
        if (type === 'executable') {
            title = 'Select Executable File';
        } else if (type === 'modsPath') {
            title = 'Select Mods Folder';
        } else if (type === 'gamePath') {
            title = 'Select Game Folder';
        } else if (type === 'file') {
            title = 'Select File';
        } else if (type === 'folder') {
            title = 'Select Folder';
        }
        return title;
    }

    // 加载驱动器列表
    loadDrives() {
        fetch('/api/filebrowser/drives')
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    const driveSelector = document.getElementById('driveSelector');
                    driveSelector.innerHTML = '';
                    
                    // 保存驱动器数据
                    this.driveData = data.drives;
                    
                    // 添加驱动器选项
                    data.drives.forEach(drive => {
                        const option = document.createElement('option');
                        option.value = drive.name;
                        // 显示驱动器名称、卷标和文件系统
                        option.textContent = `${drive.name} (${drive.volumeLabel || '无卷标'}) [${drive.fileSystem}]`;
                        driveSelector.appendChild(option);
                    });
                }
            });
    }

    // 加载应用运行目录
    loadAppDirectory() {
        fetch('/api/filebrowser/appdirectory')
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    this.loadDirectoryFiles(data.path);
                } else {
                    alert('Failed to get application directory: ' + data.message);
                    this.close();
                }
            })
            .catch(error => {
                alert('Failed to get application directory: ' + error.message);
                this.close();
            });
    }

    // 获取当前目录文件列表
    loadDirectoryFiles(directory) {
        this.currentDirectory = directory;
        const filesList = document.getElementById('filesList');
        const addressBar = document.getElementById('addressBar');
        
        addressBar.value = directory; // 同步地址栏与当前目录
        filesList.innerHTML = '<div class="text-center"><div class="spinner-border" role="status"><span class="visually-hidden">Loading...</span></div></div>';
        
        fetch(`/api/filebrowser/list?path=${encodeURIComponent(directory)}`)
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    filesList.innerHTML = '';
                    
                    // 添加返回上一级目录按钮
                    if (data.parentDirectory) {
                        const parentItem = document.createElement('div');
                        parentItem.className = 'file-item directory';
                        parentItem.innerHTML = '<span class="file-icon"><i class="bi bi-arrow-up-circle"></i></span><span class="file-name"> ..</span>';
                        parentItem.onclick = () => this.loadDirectoryFiles(data.parentDirectory);
                        filesList.appendChild(parentItem);
                    }
                    
                    // 添加目录
                    data.directories.forEach(dir => {
                        const dirItem = document.createElement('div');
                        dirItem.className = 'file-item directory';
                        dirItem.innerHTML = '<span class="file-icon"><i class="bi bi-folder"></i></span><span class="file-name">' + dir.name + '</span>';
                        
                        // 根据当前文件浏览器类型决定点击目录的行为
                        if (this.currentFileBrowserType === 'modsPath' || this.currentFileBrowserType === 'gamePath' || this.currentFileBrowserType === 'folder') {
                            // 如果是选择文件夹类型，点击目录直接选择
                            dirItem.onclick = () => this.selectFolder(dir.path);
                        } else {
                            // 如果是选择文件类型，点击目录进入目录
                            dirItem.onclick = () => this.loadDirectoryFiles(dir.path);
                        }
                        
                        filesList.appendChild(dirItem);
                    });
                    
                    // 添加文件
                    if (this.currentFileBrowserType !== 'modsPath' && this.currentFileBrowserType !== 'gamePath' && this.currentFileBrowserType !== 'folder') {
                        data.files.forEach(file => {
                            // 过滤文件类型
                            if (this.shouldShowFile(file)) {
                                const fileItem = document.createElement('div');
                                fileItem.className = 'file-item';
                                fileItem.innerHTML = `<span class="file-icon"><i class="bi bi-file-earmark"></i></span><span class="file-name">${file.name}</span>`;
                                fileItem.onclick = () => this.selectFile(file.path);
                                filesList.appendChild(fileItem);
                            }
                        });
                    }
                } else {
                    filesList.innerHTML = '<div class="text-danger">Error loading files: ' + data.message + '</div>';
                }
            })
            .catch(error => {
                filesList.innerHTML = '<div class="text-danger">Failed to load files: ' + error.message + '</div>';
            });
    }

    // 检查文件是否应该显示
    shouldShowFile(file) {
        // 如果没有指定扩展名，显示所有文件
        if (this.fileExtensions.length === 0) {
            return true;
        }
        
        // 首先检查文件扩展名是否在允许列表中
        const isAllowedExtension = this.fileExtensions.some(ext => 
            file.extension.toLowerCase() === ext.toLowerCase()
        );
        
        // 如果没有通过扩展名检查，直接返回false
        if (!isAllowedExtension) {
            return false;
        }
        
        // 根据文件过滤类型进一步过滤
        if (this.currentFileFilter === 'zip') {
            return file.extension.toLowerCase() === '.zip';
        } else if (this.currentFileFilter === 'izakaya') {
            return file.extension.toLowerCase() === '.izakaya';
        }
        
        // 默认显示所有允许的文件类型
        return true;
    }

    // 选择文件
    selectFile(filePath) {
        if (this.onFileSelected) {
            this.onFileSelected(filePath);
        } else {
            // 默认行为：如果有id为selectedFilePath的输入框，将文件路径填充到该输入框
            const selectedFilePath = document.getElementById('selectedFilePath');
            if (selectedFilePath) {
                selectedFilePath.value = filePath;
            }
        }
        this.close();
    }

    // 选择文件夹
    selectFolder(folderPath) {
        if (this.onFolderSelected) {
            this.onFolderSelected(folderPath);
        } else {
            // 默认行为：如果有id为selectedFolderPath的输入框，将文件夹路径填充到该输入框
            const selectedFolderPath = document.getElementById('selectedFolderPath');
            if (selectedFolderPath) {
                selectedFolderPath.value = folderPath;
            }
        }
        this.close();
    }

    // 处理驱动器选择
    onDriveSelected() {
        const driveSelector = document.getElementById('driveSelector');
        const selectedDrive = driveSelector.value;
        if (selectedDrive) {
            // 显示驱动器详细信息
            const driveInfo = document.getElementById('driveInfo');
            if (driveInfo) {
                // 查找选中的驱动器数据
                const drive = this.driveData.find(d => d.name === selectedDrive);
                if (drive) {
                    // 计算已用空间百分比
                    const usedSpace = drive.totalSize - drive.availableFreeSpace;
                    const usedPercentage = drive.totalSize > 0 ? Math.round((usedSpace / drive.totalSize) * 100) : 0;
                    
                    // 格式化大小信息
                    const totalSize = formatFileSize(drive.totalSize);
                    const freeSpace = formatFileSize(drive.availableFreeSpace);
                    const usedSpaceFormatted = formatFileSize(usedSpace);
                    
                    driveInfo.innerHTML = `
                        <div class="drive-details">
                            <p><strong>文件系统:</strong> ${drive.fileSystem}</p>
                            <p><strong>总容量:</strong> ${totalSize}</p>
                            <p><strong>可用空间:</strong> ${freeSpace}</p>
                            <p><strong>已用空间:</strong> ${usedSpaceFormatted} (${usedPercentage}%)</p>
                            <div class="drive-space-bar">
                                <div class="drive-space-used" style="width: ${usedPercentage}%"></div>
                            </div>
                        </div>
                    `;
                } else {
                    driveInfo.innerHTML = '';
                }
            }
            
            this.loadDirectoryFiles(selectedDrive);
        }
    }

    // 处理地址栏导航
    navigateToAddress() {
        const addressBar = document.getElementById('addressBar');
        const path = addressBar.value;
        if (path) {
            this.loadDirectoryFiles(path);
        }
    }

    // 处理文件过滤选择
    onFileFilterChange() {
        const fileFilterSelect = document.getElementById('fileFilterSelect');
        if (fileFilterSelect) {
            this.currentFileFilter = fileFilterSelect.value;
            // 如果当前正在浏览目录，重新加载文件
            if (this.currentDirectory) {
                this.loadDirectoryFiles(this.currentDirectory);
            }
        }
    }
}

// 创建全局文件浏览器实例
console.log('Creating global file browser instance...');
window.fileBrowser = new FileBrowser();
console.log('Global file browser instance created:', window.fileBrowser);

// DOM加载完成后初始化
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        console.log('DOM loaded, initializing file browser...');
        window.fileBrowser.init();
        console.log('File browser initialized');
        
        // 绑定地址栏事件
        const addressBar = document.getElementById('addressBar');
        if (addressBar) {
            addressBar.addEventListener('keypress', (e) => {
                if (e.key === 'Enter') {
                    window.fileBrowser.navigateToAddress();
                }
            });
        }
        
        // 绑定文件过滤选择事件
        const fileFilterSelect = document.getElementById('fileFilterSelect');
        if (fileFilterSelect) {
            fileFilterSelect.addEventListener('change', () => {
                window.fileBrowser.onFileFilterChange();
            });
        }
        
        // 绑定驱动器选择事件
        const driveSelector = document.getElementById('driveSelector');
        if (driveSelector) {
            driveSelector.addEventListener('change', () => {
                window.fileBrowser.onDriveSelected();
            });
        }
    });
} else {
    console.log('DOM already loaded, initializing file browser...');
    window.fileBrowser.init();
    console.log('File browser initialized');
}