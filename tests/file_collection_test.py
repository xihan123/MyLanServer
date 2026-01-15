#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
文件收集功能测试脚本
支持模板下载、数据生成、文件提交、批量提交、并发测试、全面错误测试
"""

import os
import sys
import time
import random
import asyncio
import argparse
from datetime import datetime
from typing import Dict, List, Any, Optional, Tuple
from io import BytesIO

# 添加项目根目录到路径
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from utils.test_base import TestConfig, TestResult, TestCase, MockDatabase, BATCH_CONFIGS
from utils.http_client import HttpClient, AsyncHttpClient
from utils.excel_generator import ExcelDataGenerator
from utils.file_utils import FileUtils
from utils.report_generator import ReportGenerator


# ==================== 测试用例 ====================

class TaskInfoTest(TestCase):
    """任务信息获取测试"""

    def __init__(self):
        super().__init__('任务信息获取测试', '测试获取任务基本信息接口')

    def execute(self, client: HttpClient, config: TestConfig, db: MockDatabase) -> TestResult:
        start_time = time.time()

        success, data, duration = client.get_task_info()

        if success:
            # 验证响应数据
            required_fields = ['id', 'slug', 'title', 'taskType', 'hasPassword', 'isActive']
            all_fields_present = all(field in data for field in required_fields)

            if all_fields_present:
                # 保存任务信息到数据库
                db.insert('test_submissions', {
                    'task_id': data.get('id'),
                    'task_title': data.get('title'),
                    'task_type': data.get('taskType'),
                    'has_password': data.get('hasPassword'),
                    'is_active': data.get('isActive'),
                    'status': 'task_info_test'
                })

                return TestResult(
                    test_name=self.name,
                    passed=True,
                    message='任务信息获取成功',
                    duration=duration,
                    response_data=data,
                    details={
                        'task_title': data.get('title'),
                        'task_type': '文件收集' if data.get('taskType') == 0 else '在线填表',
                        'has_password': data.get('hasPassword'),
                        'is_active': data.get('isActive'),
                        'allowed_extensions': data.get('allowedExtensions', []),
                        'allow_attachment_upload': data.get('allowAttachmentUpload', False)
                    }
                )

        return TestResult(
            test_name=self.name,
            passed=False,
            message='任务信息获取失败',
            duration=duration,
            response_data=data if success else None,
            error=data.get('error', '未知错误') if not success else '响应格式验证失败'
        )


class TemplateDownloadTest(TestCase):
    """模板下载测试"""

    def __init__(self):
        super().__init__('模板下载测试', '测试下载 Excel 模板接口')
        self.template_content = None
        self.template_filename = None

    def execute(self, client: HttpClient, config: TestConfig, db: MockDatabase) -> TestResult:
        start_time = time.time()

        success, content, duration = client.download_template()

        if success and len(content) > 0:
            # 保存模板文件
            self.template_content = content
            self.template_filename = f'template_{config.slug}.xlsx'
            save_path = os.path.join(config.downloads_dir, self.template_filename)

            success_save, message = FileUtils.save_file(save_path, content)

            if success_save:
                # 保存到数据库
                db.insert('test_submissions', {
                    'filename': self.template_filename,
                    'file_size': len(content),
                    'status': 'template_download_test'
                })

                return TestResult(
                    test_name=self.name,
                    passed=True,
                    message=f'模板下载成功，文件大小: {FileUtils.format_file_size(len(content))}',
                    duration=duration,
                    details={
                        'filename': self.template_filename,
                        'file_size': len(content),
                        'file_size_formatted': FileUtils.format_file_size(len(content)),
                        'save_path': save_path
                    }
                )
            else:
                return TestResult(
                    test_name=self.name,
                    passed=False,
                    message=f'模板下载成功，但保存失败: {message}',
                    duration=duration,
                    error=message
                )

        return TestResult(
            test_name=self.name,
            passed=False,
            message='模板下载失败',
            duration=duration,
            error='下载内容为空' if success else '下载请求失败'
        )


class DataGenerationTest(TestCase):
    """数据生成测试"""

    def __init__(self, template_content: bytes):
        super().__init__('数据生成测试', '测试基于模板生成模拟数据')
        self.template_content = template_content

    def execute(self, client: HttpClient, config: TestConfig, db: MockDatabase) -> TestResult:
        start_time = time.time()

        try:
            # 创建 Excel 数据生成器
            generator = ExcelDataGenerator.create_from_template_content(self.template_content)

            # 获取表头
            headers = generator.get_headers()
            field_types = generator.get_field_types()

            # 生成测试数据（10行）
            test_data = generator.generate_rows(10)

            # 生成 Excel 文件
            excel_content = generator.generate_excel_file(test_data, include_header=True)

            # 保存生成的文件
            filename = f'test_data_{config.slug}.xlsx'
            save_path = os.path.join(config.test_files_dir, filename)

            success_save, message = FileUtils.save_file(save_path, excel_content)

            if success_save:
                # 保存到数据库
                db.insert('test_submissions', {
                    'filename': filename,
                    'row_count': len(test_data),
                    'column_count': len(headers),
                    'file_size': len(excel_content),
                    'status': 'data_generation_test'
                })

                return TestResult(
                    test_name=self.name,
                    passed=True,
                    message=f'数据生成成功，生成 {len(test_data)} 行数据',
                    duration=time.time() - start_time,
                    details={
                        'headers': headers,
                        'field_types': field_types,
                        'row_count': len(test_data),
                        'column_count': len(headers),
                        'file_size': len(excel_content),
                        'file_size_formatted': FileUtils.format_file_size(len(excel_content)),
                        'sample_data': test_data[:3]  # 前3行数据作为示例
                    }
                )
            else:
                return TestResult(
                    test_name=self.name,
                    passed=False,
                    message=f'数据生成成功，但保存失败: {message}',
                    duration=time.time() - start_time,
                    error=message
                )

        except Exception as e:
            return TestResult(
                test_name=self.name,
                passed=False,
                message=f'数据生成失败: {str(e)}',
                duration=time.time() - start_time,
                error=str(e)
            )


class FileSubmissionTest(TestCase):
    """文件提交测试（无附件）"""

    def __init__(self, name: str, description: str, template_content: bytes, row_count: int = 10):
        super().__init__(name, description)
        self.template_content = template_content
        self.row_count = row_count

    def execute(self, client: HttpClient, config: TestConfig, db: MockDatabase) -> TestResult:
        start_time = time.time()

        try:
            # 创建 Excel 数据生成器
            generator = ExcelDataGenerator.create_from_template_content(self.template_content)

            # 生成测试数据
            test_data = generator.generate_rows(self.row_count)

            # 生成 Excel 文件
            excel_content = generator.generate_excel_file(test_data, include_header=True)

            # 提交文件
            user = config.test_user
            success, response_data, duration = client.submit_file(
                name=user['name'],
                contact=user['contact'],
                department=user['department'],
                file_content=excel_content,
                file_name=f'{user["name"]}_test.xlsx',
                password=config.password
            )

            if success:
                # 保存到数据库
                db.insert('test_submissions', {
                    'submitter': user['name'],
                    'contact': user['contact'],
                    'department': user['department'],
                    'filename': response_data.get('filename', ''),
                    'row_count': self.row_count,
                    'file_size': len(excel_content),
                    'status': 'file_submission_test'
                })

                return TestResult(
                    test_name=self.name,
                    passed=True,
                    message=f'文件提交成功: {response_data.get("filename", "")}',
                    duration=duration,
                    response_data=response_data,
                    details={
                        'submitter': user['name'],
                        'contact': user['contact'],
                        'department': user['department'],
                        'filename': response_data.get('filename', ''),
                        'row_count': self.row_count,
                        'file_size': len(excel_content)
                    }
                )
            else:
                return TestResult(
                    test_name=self.name,
                    passed=False,
                    message='文件提交失败',
                    duration=duration,
                    response_data=response_data if success else None,
                    error=response_data.get('error', '未知错误') if not success else '提交失败'
                )

        except Exception as e:
            return TestResult(
                test_name=self.name,
                passed=False,
                message=f'文件提交异常: {str(e)}',
                duration=time.time() - start_time,
                error=str(e)
            )


class FileWithAttachmentTest(TestCase):
    """文件提交测试（带附件）"""

    def __init__(self, name: str, description: str, template_content: bytes, attachment_count: int = 1):
        super().__init__(name, description)
        self.template_content = template_content
        self.attachment_count = attachment_count

    def execute(self, client: HttpClient, config: TestConfig, db: MockDatabase) -> TestResult:
        start_time = time.time()

        try:
            # 获取任务信息
            success, task_info, _ = client.get_task_info()
            if success:
                allowed_extensions = task_info.get('allowedExtensions', [])
            else:
                allowed_extensions = ['.xlsx', '.xls']  # 默认值

            # 创建 Excel 数据生成器
            generator = ExcelDataGenerator.create_from_template_content(self.template_content)

            # 生成测试数据
            test_data = generator.generate_rows(10)

            # 生成 Excel 文件
            excel_content = generator.generate_excel_file(test_data, include_header=True)

            # 根据允许的扩展名生成附件
            attachments = []
            for i in range(self.attachment_count):
                # 优先使用 Excel 格式
                if '.xlsx' in allowed_extensions:
                    att_filename = f'attachment_{i+1}.xlsx'
                    att_generator = ExcelDataGenerator()
                    att_generator.headers = ['内容']
                    att_generator.field_types = {'内容': 'text'}
                    att_data = [{'内容': f'测试附件内容 {i+1}'}]
                    att_content = att_generator.generate_excel_file(att_data, include_header=True)
                    attachments.append((att_filename, att_content))
                elif '.xls' in allowed_extensions:
                    att_filename = f'attachment_{i+1}.xls'
                    att_generator = ExcelDataGenerator()
                    att_generator.headers = ['内容']
                    att_generator.field_types = {'内容': 'text'}
                    att_data = [{'内容': f'测试附件内容 {i+1}'}]
                    att_content = att_generator.generate_excel_file(att_data, include_header=True)
                    attachments.append((att_filename, att_content))
                else:
                    # 如果没有支持的格式，使用第一个允许的格式（如果有的话）
                    if allowed_extensions:
                        ext = allowed_extensions[0]
                        att_filename = f'attachment_{i+1}{ext}'
                        att_content = f'测试附件内容 {i+1}\n' * 100
                        attachments.append((att_filename, att_content.encode('utf-8')))

            # 提交文件（带附件）
            user = config.test_user
            success, response_data, duration = client.submit_file(
                name=user['name'],
                contact=user['contact'],
                department=user['department'],
                file_content=excel_content,
                file_name=f'{user["name"]}_with_attachments.xlsx',
                password=config.password,
                attachments=attachments
            )

            if success:
                # 保存到数据库
                db.insert('test_submissions', {
                    'submitter': user['name'],
                    'contact': user['contact'],
                    'department': user['department'],
                    'filename': response_data.get('filename', ''),
                    'attachment_count': self.attachment_count,
                    'status': 'file_with_attachment_test'
                })

                return TestResult(
                    test_name=self.name,
                    passed=True,
                    message=f'文件提交成功（带 {self.attachment_count} 个附件）: {response_data.get("filename", "")}',
                    duration=duration,
                    response_data=response_data,
                    details={
                        'submitter': user['name'],
                        'contact': user['contact'],
                        'department': user['department'],
                        'filename': response_data.get('filename', ''),
                        'attachment_count': self.attachment_count,
                        'attachments': [att[0] for att in attachments]
                    }
                )
            else:
                return TestResult(
                    test_name=self.name,
                    passed=False,
                    message='文件提交失败',
                    duration=duration,
                    response_data=response_data if success else None,
                    error=response_data.get('error', '未知错误') if not success else '提交失败'
                )

        except Exception as e:
            return TestResult(
                test_name=self.name,
                passed=False,
                message=f'文件提交异常: {str(e)}',
                duration=time.time() - start_time,
                error=str(e)
            )


class BatchSubmissionTest(TestCase):
    """批量提交测试"""

    def __init__(self, count: int, template_content: bytes):
        super().__init__('批量提交测试', f'测试批量提交 {count} 个文件')
        self.count = count
        self.template_content = template_content

    def execute(self, client: HttpClient, config: TestConfig, db: MockDatabase) -> TestResult:
        start_time = time.time()

        try:
            # 创建 Excel 数据生成器
            generator = ExcelDataGenerator.create_from_template_content(self.template_content)

            success_count = 0
            failed_count = 0
            results = []

            for i in range(self.count):
                # 生成测试数据
                test_data = generator.generate_rows(10)

                # 生成 Excel 文件
                excel_content = generator.generate_excel_file(test_data, include_header=True)

                # 提交文件
                user = config.test_user
                success, response_data, _ = client.submit_file(
                    name=f'{user["name"]}_{i+1}',
                    contact=str(int(user["contact"]) + i),
                    department=user['department'],
                    file_content=excel_content,
                    file_name=f'{user["name"]}_{i+1}.xlsx',
                    password=config.password
                )

                if success:
                    success_count += 1
                    results.append({
                        'index': i+1,
                        'status': 'success',
                        'filename': response_data.get('filename', '')
                    })
                else:
                    failed_count += 1
                    results.append({
                        'index': i+1,
                        'status': 'failed',
                        'error': response_data.get('error', '未知错误')
                    })

            # 保存到数据库
            db.insert('test_submissions', {
                'total_count': self.count,
                'success_count': success_count,
                'failed_count': failed_count,
                'status': 'batch_submission_test'
            })

            return TestResult(
                test_name=self.name,
                passed=success_count > 0,
                message=f'批量提交完成: {success_count}/{self.count} 成功',
                duration=time.time() - start_time,
                details={
                    'total_count': self.count,
                    'success_count': success_count,
                    'failed_count': failed_count,
                    'success_rate': f'{success_count / self.count * 100:.1f}%',
                    'results': results[:10]  # 只显示前10个结果
                }
            )

        except Exception as e:
            return TestResult(
                test_name=self.name,
                passed=False,
                message=f'批量提交异常: {str(e)}',
                duration=time.time() - start_time,
                error=str(e)
            )


class ConcurrentSubmissionTest(TestCase):
    """并发提交测试"""

    def __init__(self, count: int, concurrent: int, template_content: bytes):
        super().__init__('并发提交测试', f'测试并发提交 {count} 个文件，{concurrent} 并发')
        self.count = count
        self.concurrent = concurrent
        self.template_content = template_content

    def execute(self, client: HttpClient, config: TestConfig, db: MockDatabase) -> TestResult:
        start_time = time.time()

        async def submit_async(index: int, async_client: AsyncHttpClient) -> Dict:
            """异步提交单个文件"""
            try:
                # 创建 Excel 数据生成器
                generator = ExcelDataGenerator.create_from_template_content(self.template_content)

                # 生成测试数据
                test_data = generator.generate_rows(10)

                # 生成 Excel 文件
                excel_content = generator.generate_excel_file(test_data, include_header=True)

                # 提交文件
                user = config.test_user
                success, response_data, duration = await async_client.submit_file_async(
                    name=f'{user["name"]}_async_{index+1}',
                    contact=str(int(user["contact"]) + index),
                    department=user['department'],
                    file_content=excel_content,
                    file_name=f'{user["name"]}_async_{index+1}.xlsx',
                    password=config.password
                )

                return {
                    'index': index+1,
                    'success': success,
                    'duration': duration,
                    'filename': response_data.get('filename', '') if success else '',
                    'error': response_data.get('error', '') if not success else ''
                }
            except Exception as e:
                return {
                    'index': index+1,
                    'success': False,
                    'duration': 0,
                    'error': str(e)
                }

        async def run_concurrent():
            """运行并发提交"""
            async_client = AsyncHttpClient(config)
            tasks = [submit_async(i, async_client) for i in range(self.count)]
            return await asyncio.gather(*tasks)

        try:
            # 运行并发提交
            results = asyncio.run(run_concurrent())

            # 统计结果
            success_count = sum(1 for r in results if r['success'])
            failed_count = len(results) - success_count
            total_duration = sum(r['duration'] for r in results)

            # 保存到数据库
            db.insert('test_submissions', {
                'total_count': self.count,
                'concurrent': self.concurrent,
                'success_count': success_count,
                'failed_count': failed_count,
                'total_duration': total_duration,
                'status': 'concurrent_submission_test'
            })

            return TestResult(
                test_name=self.name,
                passed=success_count > 0,
                message=f'并发提交完成: {success_count}/{self.count} 成功',
                duration=time.time() - start_time,
                details={
                    'total_count': self.count,
                    'concurrent': self.concurrent,
                    'success_count': success_count,
                    'failed_count': failed_count,
                    'success_rate': f'{success_count / self.count * 100:.1f}%',
                    'avg_duration': f'{total_duration / self.count:.2f}s',
                    'results': results[:10]  # 只显示前10个结果
                }
            )

        except Exception as e:
            return TestResult(
                test_name=self.name,
                passed=False,
                message=f'并发提交异常: {str(e)}',
                duration=time.time() - start_time,
                error=str(e)
            )


class ErrorHandlingTest(TestCase):
    """错误处理测试"""

    def __init__(self, template_content: bytes):
        super().__init__('错误处理测试', '测试各种错误场景')
        self.template_content = template_content

    def execute(self, client: HttpClient, config: TestConfig, db: MockDatabase) -> TestResult:
        start_time = time.time()

        # 定义错误测试场景
        error_scenarios = [
            {
                'name': '密码错误测试',
                'test': lambda: client.submit_file(
                    name=config.test_user['name'],
                    contact=config.test_user['contact'],
                    department=config.test_user['department'],
                    file_content=self.template_content,
                    file_name='test.xlsx',
                    password='wrong_password'  # 错误密码
                ),
                'expected_status': 'fail'
            },
            {
                'name': '联系方式过长测试',
                'test': lambda: client.submit_file(
                    name=config.test_user['name'],
                    contact='12345678901234567890',  # 超过15位
                    department=config.test_user['department'],
                    file_content=self.template_content,
                    file_name='test.xlsx',
                    password=config.password
                ),
                'expected_status': 'fail'
            },
            {
                'name': '联系方式过短测试',
                'test': lambda: client.submit_file(
                    name=config.test_user['name'],
                    contact='12',  # 少于3位
                    department=config.test_user['department'],
                    file_content=self.template_content,
                    file_name='test.xlsx',
                    password=config.password
                ),
                'expected_status': 'fail'
            },
            {
                'name': '文件格式错误测试',
                'test': lambda: client.submit_file(
                    name=config.test_user['name'],
                    contact=config.test_user['contact'],
                    department=config.test_user['department'],
                    file_content=b'invalid content',  # 无效的 Excel 内容
                    file_name='test.txt',  # 错误的扩展名
                    password=config.password
                ),
                'expected_status': 'fail'
            }
        ]

        test_results = []

        for scenario in error_scenarios:
            try:
                success, response_data, _ = scenario['test']()

                test_passed = (scenario['expected_status'] == 'fail' and not success) or \
                             (scenario['expected_status'] == 'success' and success)

                test_results.append({
                    'scenario': scenario['name'],
                    'passed': test_passed,
                    'expected': scenario['expected_status'],
                    'actual': 'fail' if not success else 'success',
                    'error': response_data.get('error') if not success else None
                })

                # 保存到模拟数据库
                db.insert('test_errors', {
                    'scenario': scenario['name'],
                    'passed': test_passed,
                    'error': response_data.get('error') if not success else None,
                    'status': 'error_test'
                })

            except Exception as e:
                test_results.append({
                    'scenario': scenario['name'],
                    'passed': False,
                    'expected': scenario['expected_status'],
                    'actual': 'exception',
                    'error': str(e)
                })

        total_duration = time.time() - start_time
        passed_count = sum(1 for r in test_results if r['passed'])

        return TestResult(
            test_name=self.name,
            passed=passed_count > 0,
            message=f'错误测试通过 {passed_count}/{len(test_results)}',
            duration=total_duration,
            details={
                'total_scenarios': len(error_scenarios),
                'passed_scenarios': passed_count,
                'failed_scenarios': len(error_scenarios) - passed_count,
                'results': test_results
            }
        )


# ==================== 测试执行引擎 ====================

class TestRunner:
    """测试执行引擎"""

    def __init__(self, config: TestConfig):
        self.config = config
        self.client = HttpClient(config)
        self.db = MockDatabase()
        self.results: List[TestResult] = []
        self.test_cases: List[TestCase] = []  # 保存测试用例实例
        self.template_content = None
        self.task_info = None

    def register_test(self, test_case: TestCase) -> TestResult:
        """注册并执行测试用例"""
        test_case.setup()
        result = test_case.execute(self.client, self.config, self.db)
        test_case.teardown()
        self.results.append(result)
        self.test_cases.append(test_case)  # 保存测试用例实例
        return result

    def run_all(self) -> List[TestResult]:
        """运行所有测试"""
        print(f'\n开始文件收集功能测试...')
        print(f'Base API: {self.config.base_api}')
        print(f'Slug: {self.config.slug}')
        print(f'批量提交: {self.config.batch_count} 次, {self.config.concurrent} 并发')
        print(f'-' * 60)

        # 1. 任务信息获取测试
        result = self.register_test(TaskInfoTest())
        print(f'✓ {result.test_name}: {"通过" if result.passed else "失败"} ({result.duration:.2f}s)')
        if result.passed:
            self.task_info = result.response_data

        # 2. 模板下载测试
        template_test = TemplateDownloadTest()
        result = self.register_test(template_test)
        print(f'✓ {result.test_name}: {"通过" if result.passed else "失败"} ({result.duration:.2f}s)')
        if result.passed and hasattr(template_test, 'template_content'):
            self.template_content = template_test.template_content

        # 3. 数据生成测试
        if self.template_content:
            result = self.register_test(DataGenerationTest(self.template_content))
            print(f'✓ {result.test_name}: {"通过" if result.passed else "失败"} ({result.duration:.2f}s)')

        # 4. 文件提交测试（无附件）
        if self.template_content:
            result = self.register_test(FileSubmissionTest('文件提交测试（无附件）', '测试不带附件的文件提交', self.template_content, 10))
            print(f'✓ {result.test_name}: {"通过" if result.passed else "失败"} ({result.duration:.2f}s)')

        # 5. 文件提交测试（单个附件）
        if self.template_content:
            result = self.register_test(FileWithAttachmentTest('文件提交测试（单个附件）', '测试带单个附件的文件提交', self.template_content, 1))
            print(f'✓ {result.test_name}: {"通过" if result.passed else "失败"} ({result.duration:.2f}s)')

        # 6. 文件提交测试（多个附件）
        if self.template_content:
            result = self.register_test(FileWithAttachmentTest('文件提交测试（多个附件）', '测试带多个附件的文件提交', self.template_content, 3))
            print(f'✓ {result.test_name}: {"通过" if result.passed else "失败"} ({result.duration:.2f}s)')

        # 7. 批量提交测试
        if self.template_content:
            result = self.register_test(BatchSubmissionTest(self.config.batch_count, self.template_content))
            print(f'✓ {result.test_name}: {"通过" if result.passed else "失败"} ({result.duration:.2f}s)')

        # 8. 并发提交测试
        if self.template_content:
            result = self.register_test(ConcurrentSubmissionTest(self.config.batch_count, self.config.concurrent, self.template_content))
            print(f'✓ {result.test_name}: {"通过" if result.passed else "失败"} ({result.duration:.2f}s)')

        # 9. 错误处理测试
        if self.template_content:
            result = self.register_test(ErrorHandlingTest(self.template_content))
            print(f'✓ {result.test_name}: {"通过" if result.passed else "失败"} ({result.duration:.2f}s)')

        print(f'-' * 60)
        print(f'测试完成!')

        return self.results


# ==================== 主程序入口 ====================

def main():
    """主程序入口"""
    parser = argparse.ArgumentParser(description='文件收集功能测试脚本')

    # 必需参数
    parser.add_argument('--base-api', default='http://192.168.0.100:8080', help='Base API URL (e.g., http://192.168.0.100:8080)')
    parser.add_argument('--slug', required=True, help='Task Slug')
    parser.add_argument('--password', required=True, help='Task Password')

    # 可选参数
    parser.add_argument('--name', default='测试用户', help='Test user name')
    parser.add_argument('--contact', default='12345678901', help='Test user contact')
    parser.add_argument('--department', default='中部', help='Test user department')
    parser.add_argument('--batch', choices=['single', 'small', 'medium', 'large'], default='small', help='Batch size preset')
    parser.add_argument('--count', type=int, help='Custom batch count')
    parser.add_argument('--concurrent', type=int, help='Custom concurrent count')
    parser.add_argument('--row-count', type=int, default=10, help='Number of rows to generate in Excel file')
    parser.add_argument('--output-dir', default='test_reports', help='Output directory for reports')
    parser.add_argument('--no-files', action='store_true', help='Skip file generation tests')

    args = parser.parse_args()

    # 确定批量配置
    if args.count:
        batch_count = args.count
    else:
        batch_count = BATCH_CONFIGS[args.batch]['count']

    if args.concurrent:
        concurrent = args.concurrent
    else:
        concurrent = BATCH_CONFIGS[args.batch]['concurrent']

    # 创建测试配置
    config = TestConfig(
        base_api=args.base_api,
        slug=args.slug,
        password=args.password,
        test_user={
            'name': args.name,
            'contact': args.contact,
            'department': args.department
        },
        batch_count=batch_count,
        concurrent=concurrent,
        output_dir=args.output_dir
    )

    # 运行测试
    runner = TestRunner(config)
    results = runner.run_all()

    # 生成报告
    report_generator = ReportGenerator(config.output_dir)

    # 准备额外信息
    additional_info = {}
    if runner.task_info:
        additional_info['task_info'] = runner.task_info

    # 生成报告内容
    report_content = report_generator.generate_markdown_report(results, config, additional_info)

    # 保存报告
    timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
    report_filename = f'file_collection_test_report_{timestamp}.md'
    report_path = report_generator.save_report(report_content, report_filename)

    print(f'\n测试报告已保存: {report_path}')

    # 显示测试结果摘要
    passed_count = sum(1 for r in results if r.passed)
    print(f'\n测试结果摘要:')
    print(f'总测试数: {len(results)}')
    print(f'通过数: {passed_count}')
    print(f'失败数: {len(results) - passed_count}')
    print(f'通过率: {passed_count / len(results) * 100:.1f}%')


if __name__ == '__main__':
    main()
