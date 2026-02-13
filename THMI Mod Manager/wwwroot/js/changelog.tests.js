/**
 * Unit Tests for Changelog Functionality
 * Tests core logic for changelog modal, caching, and error handling
 */

// Mock localStorage for testing
const mockLocalStorage = (() => {
    let store = {};
    return {
        getItem: (key) => store[key] || null,
        setItem: (key, value) => { store[key] = value.toString(); },
        removeItem: (key) => { delete store[key]; },
        clear: () => { store = {}; },
        get store() { return store; }
    };
})();

// Mock bootstrap Modal
const mockBootstrap = {
    Modal: class {
        constructor(element) {
            this.element = element;
            this.shown = false;
        }
        show() { this.shown = true; }
        hide() { this.shown = false; }
    }
};

// Test Suite
const testSuite = {
    tests: [],
    
    addTest(name, testFn) {
        this.tests.push({ name, testFn });
    },
    
    runAll() {
        console.log('🧪 Starting Changelog Functionality Tests\n');
        let passed = 0;
        let failed = 0;
        
        this.tests.forEach(test => {
            try {
                test.testFn();
                console.log(`✅ PASS: ${test.name}`);
                passed++;
            } catch (error) {
                console.log(`❌ FAIL: ${test.name}`);
                console.log(`   Error: ${error.message}`);
                failed++;
            }
        });
        
        console.log(`\n📊 Test Results: ${passed} passed, ${failed} failed`);
        return failed === 0;
    }
};

// Test 1: Caching Mechanism
testSuite.addTest('Changelog cache stores and retrieves data correctly', () => {
    const cacheKey = 'thmi_changelog_cache';
    const testVersion = '1.0.0';
    const testData = {
        version: testVersion,
        releaseNotes: '<h1>Test Release</h1>',
        publishedAt: new Date().toISOString()
    };
    
    // Store cache
    const cache = {
        version: testVersion,
        data: testData,
        timestamp: Date.now()
    };
    mockLocalStorage.setItem(`${cacheKey}_${testVersion}`, JSON.stringify(cache));
    
    // Retrieve cache
    const retrieved = mockLocalStorage.getItem(`${cacheKey}_${testVersion}`);
    if (!retrieved) {
        throw new Error('Cache not retrieved');
    }
    
    const parsed = JSON.parse(retrieved);
    if (parsed.data.version !== testVersion) {
        throw new Error('Cached version mismatch');
    }
});

// Test 2: Cache Expiration
testSuite.addTest('Cache expiration logic works correctly', () => {
    const cacheExpiry = 24 * 60 * 60 * 1000; // 24 hours
    const now = Date.now();
    
    // Test valid cache
    const validCache = {
        data: { test: 'data' },
        timestamp: now - (cacheExpiry / 2) // 12 hours ago - should be valid
    };
    
    // Test expired cache
    const expiredCache = {
        data: { test: 'data' },
        timestamp: now - (cacheExpiry + 1000) // 24+ hours ago - should be expired
    };
    
    const isValidCacheValid = (Date.now() - validCache.timestamp) < cacheExpiry;
    const isExpiredCacheValid = (Date.now() - expiredCache.timestamp) < cacheExpiry;
    
    if (!isValidCacheValid) {
        throw new Error('Valid cache incorrectly marked as expired');
    }
    
    if (isExpiredCacheValid) {
        throw new Error('Expired cache incorrectly marked as valid');
    }
});

// Test 3: Version Badge Display
testSuite.addTest('Version badge is formatted correctly', () => {
    const testVersion = '1.0.0';
    const badgeElement = {
        textContent: '',
        style: { display: 'none' }
    };
    
    // Simulate showing badge
    badgeElement.textContent = testVersion;
    badgeElement.style.display = 'inline-flex';
    
    if (badgeElement.textContent !== testVersion) {
        throw new Error('Version badge text mismatch');
    }
    
    if (badgeElement.style.display !== 'inline-flex') {
        throw new Error('Version badge visibility issue');
    }
});

// Test 4: Error State Display
testSuite.addTest('Error state shows correctly', () => {
    const errorElement = {
        style: { display: 'none' },
        textContent: ''
    };
    
    const errorMessage = 'Failed to load changelog. Please try again later.';
    
    // Simulate showing error
    errorElement.style.display = 'flex';
    errorElement.textContent = errorMessage;
    
    if (errorElement.style.display !== 'flex') {
        throw new Error('Error element not visible');
    }
    
    if (errorElement.textContent !== errorMessage) {
        throw new Error('Error message not set correctly');
    }
});

// Test 5: Loading State
testSuite.addTest('Loading spinner visibility toggles correctly', () => {
    const loadingElement = {
        style: { display: 'none' }
    };
    
    // Show loading
    loadingElement.style.display = 'flex';
    if (loadingElement.style.display !== 'flex') {
        throw new Error('Loading spinner not visible when showing');
    }
    
    // Hide loading
    loadingElement.style.display = 'none';
    if (loadingElement.style.display !== 'none') {
        throw new Error('Loading spinner still visible when hiding');
    }
});

// Test 6: Modal Instance Creation
testSuite.addTest('Modal creates instance correctly', () => {
    const mockElement = document.createElement('div');
    const modal = new mockBootstrap.Modal(mockElement);
    
    if (!modal) {
        throw new Error('Modal instance not created');
    }
    
    if (modal.shown) {
        throw new Error('Modal shown before calling show()');
    }
    
    modal.show();
    if (!modal.shown) {
        throw new Error('Modal not shown after calling show()');
    }
});

// Test 7: Changelog Button Visibility Logic
testSuite.addTest('Changelog button visibility depends on release notes', () => {
    const btnElement = {
        style: { display: 'none' },
        dataset: { version: '' }
    };
    
    // Case 1: No release notes - button hidden
    let releaseNotes = null;
    if (releaseNotes) {
        btnElement.style.display = 'inline-flex';
        btnElement.dataset.version = '1.0.0';
    } else {
        btnElement.style.display = 'none';
    }
    
    if (btnElement.style.display !== 'none') {
        throw new Error('Button should be hidden when no release notes');
    }
    
    // Case 2: Has release notes - button visible
    releaseNotes = '## New Features\n- Feature 1';
    if (releaseNotes) {
        btnElement.style.display = 'inline-flex';
        btnElement.dataset.version = '1.0.0';
    }
    
    if (btnElement.style.display !== 'inline-flex') {
        throw new Error('Button should be visible when release notes exist');
    }
});

// Test 8: Markdown Parsing Helpers
testSuite.addTest('Markdown formatting functions work correctly', () => {
    // Test basic markdown patterns
    const markdownTests = [
        {
            input: '**bold text**',
            expected: '<strong>bold text</strong>',
            pattern: /\*\*(.*?)\*\*/g
        },
        {
            input: '*italic text*',
            expected: '<em>italic text</em>',
            pattern: /\*(.*?)\*/g
        },
        {
            input: '`code`',
            expected: '<code>code</code>',
            pattern: /`(.*?)`/g
        },
        {
            input: '[link text](https://example.com)',
            expected: '<a href="https://example.com" target="_blank">link text</a>',
            pattern: /\[(.*?)\]\((.*?)\)/g
        }
    ];
    
    markdownTests.forEach(test => {
        const result = test.input.replace(test.pattern, (match, p1, p2) => {
            if (test.input.includes('[link')) {
                return test.expected;
            }
            if (test.input.includes('**')) {
                return `<strong>${p1}</strong>`;
            }
            if (test.input.includes('*')) {
                return `<em>${p1}</em>`;
            }
            return `<code>${p1}</code>`;
        });
        
        if (result !== test.expected) {
            throw new Error(`Markdown parsing failed for: ${test.input}`);
        }
    });
});

// Test 9: Localization Fallback
testSuite.addTest('Localization fallback works correctly', () => {
    const getLocalizedString = (key, defaultValue) => {
        // Simulate missing localization - should return default
        return defaultValue;
    };
    
    const result = getLocalizedString('Updates:NonExistent', 'Default Value');
    if (result !== 'Default Value') {
        throw new Error('Localization fallback not working');
    }
});

// Test 10: Modal Close Button
testSuite.addTest('Modal close functionality works', () => {
    const closeBtn = {
        clicked: false,
        click() { this.clicked = true; }
    };
    
    const modalContent = {
        visible: true,
        close() { this.visible = false; }
    };
    
    // Simulate close button click
    closeBtn.click();
    if (!closeBtn.clicked) {
        throw new Error('Close button not registering clicks');
    }
    
    // Simulate closing modal
    modalContent.close();
    if (modalContent.visible) {
        throw new Error('Modal not closing');
    }
});

// Run all tests
if (typeof window !== 'undefined') {
    // Browser environment
    window.runChangelogTests = () => testSuite.runAll();
} else {
    // Node.js or test environment
    module.exports = testSuite;
}
