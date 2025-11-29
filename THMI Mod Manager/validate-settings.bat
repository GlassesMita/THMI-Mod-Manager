#!/bin/bash
# 验证设置页面功能

echo "=== 设置页面功能验证 ==="
echo

# 测试1: 检查配置API
echo "测试1: 检查配置API..."
curl -s "http://localhost:5000/api/config/get?key=%5BCursor%5DUseMystiaCursor" | python -m json.tool 2>/dev/null || echo "API调用失败"

# 测试2: 检查设置页面复选框状态
echo
echo "测试2: 检查设置页面复选框状态..."
html=$(curl -s "http://localhost:5000/Settings")

# 检查自定义指针复选框
if echo "$html" | grep -q 'id="useMystiaCursor"[^>]*checked'; then
    echo "✅ 自定义指针复选框: 已选中"
else
    echo "❌ 自定义指针复选框: 未选中"
fi

# 检查开发者模式复选框
if echo "$html" | grep -q 'id="devMode"[^>]*checked'; then
    echo "✅ 开发者模式复选框: 已选中"
else
    echo "❌ 开发者模式复选框: 未选中"
fi

# 检查CVE警告复选框
if echo "$html" | grep -q 'id="showCVEWarning"[^>]*checked'; then
    echo "✅ CVE警告复选框: 已选中"
else
    echo "❌ CVE警告复选框: 未选中"
fi

# 测试3: 检查配置文件
echo
echo "测试3: 检查配置文件..."
if [ -f "AppConfig.Schale" ]; then
    echo "配置文件存在，内容如下:"
    cat AppConfig.Schale | grep -E "(UseMystiaCursor|IsDevBuild|ShowCVEWarning)"
else
    echo "❌ 配置文件不存在"
fi

echo
echo "=== 验证完成 ==="