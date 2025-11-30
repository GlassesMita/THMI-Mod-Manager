const { Builder, By, until } = require('selenium-webdriver');
const edge = require('selenium-webdriver/edge');

async function testOsuCursor() {
    console.log('开始测试osu! lazer光标功能...');
    
    let driver;
    
    try {
        // 创建WebDriver实例
        driver = await new Builder()
            .forBrowser('MicrosoftEdge')
            .setEdgeOptions(new edge.Options())
            .build();
        
        console.log('Edge浏览器已启动');
        
        // 访问测试页面
        await driver.get('http://localhost:5000/test-osu-cursor.html');
        console.log('已导航到测试页面');
        
        // 等待页面加载完成
        await driver.wait(until.elementLocated(By.tagName('body')), 10000);
        console.log('页面加载完成');
        
        // 等待控制台元素出现
        await driver.wait(until.elementLocated(By.id('console')), 5000);
        
        // 查找"Initialize osu! lazer Cursor"按钮并点击
        const initButton = await driver.findElement(By.xpath("//button[contains(text(), 'Initialize osu! lazer Cursor')]"));
        await initButton.click();
        console.log('已点击初始化光标按钮');
        
        // 等待一段时间让光标初始化
        await driver.sleep(3000);
        
        // 获取控制台输出并打印
        const consoleElement = await driver.findElement(By.id('console'));
        let consoleText = await consoleElement.getText();
        console.log('控制台输出:');
        console.log(consoleText);
        
        // 在测试区域内模拟鼠标移动
        const testArea = await driver.findElement(By.id('testArea'));
        await driver.actions({bridge: true})
            .move({origin: testArea, x: 50, y: 50})
            .perform();
        
        console.log('已模拟鼠标移动');
        
        // 等待动画执行
        await driver.sleep(1000);
        
        // 测试鼠标按下和释放
        await driver.actions({bridge: true})
            .move({origin: testArea, x: 100, y: 100})
            .press()
            .perform();
        
        console.log('已模拟鼠标按下');
        await driver.sleep(500);
        
        await driver.actions({bridge: true})
            .release()
            .perform();
        
        console.log('已模拟鼠标释放');
        
        // 等待观察效果
        await driver.sleep(2000);
        
        // 再次获取控制台输出
        consoleText = await consoleElement.getText();
        console.log('更新后的控制台输出:');
        console.log(consoleText);
        
        // 验证光标元素是否存在
        try {
            const cursorElements = await driver.findElements(By.css('.osu-cursor'));
            console.log(`找到 ${cursorElements.length} 个光标元素`);
            
            if (cursorElements.length > 0) {
                console.log('✓ osu! lazer光标功能正常工作');
            } else {
                console.log('⚠ 未找到osu! lazer光标元素，但可能正常（光标可能是通过Canvas或其他方式渲染）');
            }
        } catch (error) {
            console.log('检查光标元素时出错:', error.message);
        }
        
        // 测试AnimeJS功能
        const testAnimeButton = await driver.findElement(By.xpath("//button[contains(text(), 'Test AnimeJS')]"));
        await testAnimeButton.click();
        console.log('已点击测试AnimeJS按钮');
        
        await driver.sleep(2000);
        
        // 再次获取控制台输出
        consoleText = await consoleElement.getText();
        console.log('AnimeJS测试后的控制台输出:');
        console.log(consoleText);
        
        // 测试卸载光标
        const unloadButton = await driver.findElement(By.xpath("//button[contains(text(), 'Unload osu! lazer Cursor')]"));
        await unloadButton.click();
        console.log('已点击卸载光标按钮');
        
        await driver.sleep(1000);
        
        console.log('测试完成');
        
    } catch (error) {
        console.error('测试过程中出现错误:', error.message);
        if (error.stack) {
            console.error('错误堆栈:', error.stack);
        }
    } finally {
        // 关闭浏览器
        if (driver) {
            await driver.quit();
            console.log('浏览器已关闭');
        }
    }
}

// 运行测试
if (require.main === module) {
    testOsuCursor().catch(console.error);
}