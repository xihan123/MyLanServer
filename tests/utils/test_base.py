#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
测试基类和通用类
包含测试配置、测试结果、测试用例基类、模拟数据库等
"""

import os
import json
from datetime import datetime
from typing import Dict, List, Any, Optional
from dataclasses import dataclass, field


# ==================== 配置模块 ====================

@dataclass
class TestConfig:
    """测试配置类"""
    base_api: str
    slug: str
    password: str
    test_user: Dict[str, str] = field(default_factory=lambda: {
        'name': '测试用户',
        'contact': '12345678901',
        'department': '技术部'
    })
    batch_count: int = 10
    concurrent: int = 1
    timeout: int = 30
    output_dir: str = 'test_reports'
    test_files_dir: str = 'test_files'
    downloads_dir: str = 'downloads'

    def __post_init__(self):
        """初始化后处理"""
        # 确保目录存在
        os.makedirs(self.output_dir, exist_ok=True)
        os.makedirs(self.test_files_dir, exist_ok=True)
        os.makedirs(self.downloads_dir, exist_ok=True)


# 预设批量配置
BATCH_CONFIGS = {
    'single': {'count': 1, 'concurrent': 1},
    'small': {'count': 10, 'concurrent': 1},
    'medium': {'count': 50, 'concurrent': 5},
    'large': {'count': 500, 'concurrent': 10}
}


# ==================== 测试结果类 ====================

@dataclass
class TestResult:
    """测试结果类"""
    test_name: str
    passed: bool
    message: str
    duration: float
    response_data: Optional[Dict] = None
    error: Optional[str] = None
    details: Optional[Dict] = None


# ==================== 测试用例基类 ====================

class TestCase:
    """测试用例基类"""

    def __init__(self, name: str, description: str):
        self.name = name
        self.description = description

    def setup(self):
        """测试前准备"""
        pass

    def execute(self, client, config: TestConfig, db: 'MockDatabase') -> TestResult:
        """执行测试"""
        raise NotImplementedError

    def teardown(self):
        """测试后清理"""
        pass


# ==================== 模拟数据库 ====================

class MockDatabase:
    """模拟数据库"""

    def __init__(self):
        self.collections = {
            'test_submissions': [],
            'test_attachments': [],
            'test_errors': []
        }

    def insert(self, collection: str, data: Dict):
        """插入数据"""
        if collection in self.collections:
            data['id'] = len(self.collections[collection]) + 1
            data['timestamp'] = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
            self.collections[collection].append(data)

    def find(self, collection: str, query: Dict) -> List[Dict]:
        """查询数据"""
        if collection not in self.collections:
            return []

        results = []
        for item in self.collections[collection]:
            match = True
            for key, value in query.items():
                if key not in item or item[key] != value:
                    match = False
                    break
            if match:
                results.append(item)
        return results

    def count(self, collection: str) -> int:
        """统计数量"""
        return len(self.collections.get(collection, []))

    def clear(self, collection: str):
        """清空集合"""
        if collection in self.collections:
            self.collections[collection].clear()

    def get_all(self, collection: str) -> List[Dict]:
        """获取所有数据"""
        return self.collections.get(collection, [])