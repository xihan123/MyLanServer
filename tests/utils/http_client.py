#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
HTTP 客户端
支持在线填表模式和文件收集模式的 API 调用
"""

import time
import json
from typing import Dict, List, Any, Optional, Tuple
from io import BytesIO

import requests
import aiohttp

from .test_base import TestConfig


class HttpClient:
    """HTTP 请求客户端（同步）"""

    def __init__(self, config: TestConfig):
        self.config = config
        self.session = requests.Session()
        self.session.headers.update({
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
            'Accept': '*/*',
            'Accept-Language': 'zh-CN,zh;q=0.9,en;q=0.8'
        })

    # ==================== 在线填表模式 API ====================

    def get_schema(self) -> Tuple[bool, Dict, float]:
        """
        获取 Schema（在线填表模式）

        Returns:
            (成功标志, 响应数据, 耗时)
        """
        url = f"{self.config.base_api}/api/distribution/{self.config.slug}/schema"
        headers = {'X-Password': self.config.password}

        start_time = time.time()
        try:
            response = self.session.get(url, headers=headers, timeout=self.config.timeout)
            duration = time.time() - start_time

            if response.status_code == 200:
                data = response.json()
                return True, data, duration
            else:
                return False, {'error': f'HTTP {response.status_code}', 'detail': response.text}, duration
        except Exception as e:
            duration = time.time() - start_time
            return False, {'error': str(e)}, duration

    def get_attachments_list(self) -> Tuple[bool, Dict, float]:
        """
        获取附件列表（在线填表模式）

        Returns:
            (成功标志, 响应数据, 耗时)
        """
        url = f"{self.config.base_api}/api/distribution/{self.config.slug}/attachments"
        headers = {'X-Password': self.config.password}

        start_time = time.time()
        try:
            response = self.session.get(url, headers=headers, timeout=self.config.timeout)
            duration = time.time() - start_time

            if response.status_code == 200:
                data = response.json()
                return True, data, duration
            else:
                return False, {'error': f'HTTP {response.status_code}', 'detail': response.text}, duration
        except Exception as e:
            duration = time.time() - start_time
            return False, {'error': str(e)}, duration

    def download_attachment(self, attachment_id: int) -> Tuple[bool, bytes, float]:
        """
        下载附件（在线填表模式）

        Args:
            attachment_id: 附件 ID

        Returns:
            (成功标志, 文件内容, 耗时)
        """
        url = f"{self.config.base_api}/api/distribution/{self.config.slug}/attachments/{attachment_id}"
        headers = {'X-Password': self.config.password}

        start_time = time.time()
        try:
            response = self.session.get(url, headers=headers, timeout=self.config.timeout)
            duration = time.time() - start_time

            if response.status_code == 200:
                return True, response.content, duration
            else:
                return False, b'', duration
        except Exception as e:
            duration = time.time() - start_time
            return False, b'', duration

    def submit_form(self, data: Dict, files: Optional[List[Tuple[str, str, bytes]]] = None) -> Tuple[bool, Dict, float]:
        """
        提交表单（在线填表模式）

        Args:
            data: 表单数据
            files: 附件列表 [(字段名, 文件名, 内容)]

        Returns:
            (成功标志, 响应数据, 耗时)
        """
        url = f"{self.config.base_api}/api/distribution/{self.config.slug}/submit"

        # 准备表单数据
        form_data = {}
        for key, value in data.items():
            if key != 'jsonData':
                form_data[key] = value

        # jsonData 需要序列化为 JSON 字符串
        if 'jsonData' in data:
            form_data['jsonData'] = json.dumps(data['jsonData'], ensure_ascii=False)

        # 添加密码
        form_data['password'] = self.config.password

        # 准备文件
        files_dict = None
        if files:
            files_dict = []
            for field_name, filename, content in files:
                # 使用 BytesIO 包装字节串，创建类文件对象
                file_obj = BytesIO(content)
                files_dict.append((field_name, (filename, file_obj)))

        start_time = time.time()
        try:
            if files_dict:
                response = self.session.post(
                    url,
                    data=form_data,
                    files=files_dict,
                    timeout=self.config.timeout
                )
            else:
                response = self.session.post(
                    url,
                    data=form_data,
                    timeout=self.config.timeout
                )

            duration = time.time() - start_time

            if response.status_code == 200:
                result = response.json()
                return True, result, duration
            else:
                return False, {'error': f'HTTP {response.status_code}', 'detail': response.text}, duration
        except Exception as e:
            duration = time.time() - start_time
            return False, {'error': str(e)}, duration

    # ==================== 文件收集模式 API ====================

    def get_task_info(self) -> Tuple[bool, Dict, float]:
        """
        获取任务信息（文件收集模式）

        Returns:
            (成功标志, 响应数据, 耗时)
        """
        url = f"{self.config.base_api}/api/task/{self.config.slug}/info"

        start_time = time.time()
        try:
            response = self.session.get(url, timeout=self.config.timeout)
            duration = time.time() - start_time

            if response.status_code == 200:
                data = response.json()
                return True, data, duration
            else:
                return False, {'error': f'HTTP {response.status_code}', 'detail': response.text}, duration
        except Exception as e:
            duration = time.time() - start_time
            return False, {'error': str(e)}, duration

    def download_template(self) -> Tuple[bool, bytes, float]:
        """
        下载 Excel 模板（文件收集模式）

        Returns:
            (成功标志, 文件内容, 耗时)
        """
        url = f"{self.config.base_api}/api/template/{self.config.slug}"
        headers = {'X-Password': self.config.password}

        start_time = time.time()
        try:
            response = self.session.get(url, headers=headers, timeout=self.config.timeout)
            duration = time.time() - start_time

            if response.status_code == 200:
                return True, response.content, duration
            else:
                return False, b'', duration
        except Exception as e:
            duration = time.time() - start_time
            return False, b'', duration

    def submit_file(
        self,
        name: str,
        contact: str,
        department: str,
        file_content: bytes,
        file_name: str,
        password: Optional[str] = None,
        attachments: Optional[List[Tuple[str, bytes]]] = None
    ) -> Tuple[bool, Dict, float]:
        """
        提交文件（文件收集模式）

        Args:
            name: 提交人姓名
            contact: 联系方式
            department: 所属部门
            file_content: Excel 文件内容
            file_name: Excel 文件名
            password: 访问密码（如果任务设置了密码）
            attachments: 附件列表 [(文件名, 内容)]

        Returns:
            (成功标志, 响应数据, 耗时)
        """
        url = f"{self.config.base_api}/api/submit/{self.config.slug}"

        # 准备表单数据
        form_data = {
            'name': name,
            'contact': contact,
            'department': department
        }

        # 添加密码（如果提供了）
        if password:
            form_data['password'] = password

        # 准备文件列表
        files = [
            ('file', (file_name, BytesIO(file_content), 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'))
        ]

        # 准备附件
        if attachments:
            for att_name, att_content in attachments:
                files.append(('attachments', (att_name, BytesIO(att_content))))

        start_time = time.time()
        try:
            response = self.session.post(
                url,
                data=form_data,
                files=files,
                timeout=self.config.timeout
            )
            duration = time.time() - start_time

            if response.status_code == 200:
                result = response.json()
                return True, result, duration
            else:
                return False, {'error': f'HTTP {response.status_code}', 'detail': response.text}, duration
        except Exception as e:
            duration = time.time() - start_time
            return False, {'error': str(e)}, duration

    def get_department_list(self) -> Tuple[bool, List[str], float]:
        """
        获取部门列表

        Returns:
            (成功标志, 部门列表, 耗时)
        """
        url = f"{self.config.base_api}/api/departments"

        start_time = time.time()
        try:
            response = self.session.get(url, timeout=self.config.timeout)
            duration = time.time() - start_time

            if response.status_code == 200:
                data = response.json()
                departments = data.get('departments', [])
                return True, departments, duration
            else:
                return False, [], duration
        except Exception as e:
            duration = time.time() - start_time
            return False, [], duration


class AsyncHttpClient:
    """HTTP 请求客户端（异步）"""

    def __init__(self, config: TestConfig):
        self.config = config
        self.headers = {
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36',
            'Accept': '*/*',
            'Accept-Language': 'zh-CN,zh;q=0.9,en;q=0.8'
        }

    # ==================== 在线填表模式 API ====================

    async def submit_form_async(self, data: Dict, files: Optional[List[Tuple[str, str, bytes]]] = None) -> Tuple[bool, Dict, float]:
        """
        异步提交表单（在线填表模式）

        Args:
            data: 表单数据
            files: 附件列表 [(字段名, 文件名, 内容)]

        Returns:
            (成功标志, 响应数据, 耗时)
        """
        url = f"{self.config.base_api}/api/distribution/{self.config.slug}/submit"

        # 准备表单数据
        form_data = aiohttp.FormData()
        for key, value in data.items():
            if key != 'jsonData':
                form_data.add_field(key, str(value))

        # jsonData 需要序列化为 JSON 字符串
        if 'jsonData' in data:
            form_data.add_field('jsonData', json.dumps(data['jsonData'], ensure_ascii=False))

        # 添加密码
        form_data.add_field('password', self.config.password)

        # 准备文件
        if files:
            for field_name, filename, content in files:
                form_data.add_field(field_name, content, filename=filename)

        start_time = time.time()
        try:
            timeout = aiohttp.ClientTimeout(total=self.config.timeout)
            async with aiohttp.ClientSession(timeout=timeout) as session:
                async with session.post(url, data=form_data) as response:
                    duration = time.time() - start_time

                    if response.status == 200:
                        result = await response.json()
                        return True, result, duration
                    else:
                        text = await response.text()
                        return False, {'error': f'HTTP {response.status}', 'detail': text}, duration
        except Exception as e:
            duration = time.time() - start_time
            return False, {'error': str(e)}, duration

    # ==================== 文件收集模式 API ====================

    async def submit_file_async(
        self,
        name: str,
        contact: str,
        department: str,
        file_content: bytes,
        file_name: str,
        password: Optional[str] = None,
        attachments: Optional[List[Tuple[str, bytes]]] = None
    ) -> Tuple[bool, Dict, float]:
        """
        异步提交文件（文件收集模式）

        Args:
            name: 提交人姓名
            contact: 联系方式
            department: 所属部门
            file_content: Excel 文件内容
            file_name: Excel 文件名
            password: 访问密码（如果任务设置了密码）
            attachments: 附件列表 [(文件名, 内容)]

        Returns:
            (成功标志, 响应数据, 耗时)
        """
        url = f"{self.config.base_api}/api/submit/{self.config.slug}"

        # 准备表单数据
        form_data = aiohttp.FormData()
        form_data.add_field('name', name)
        form_data.add_field('contact', contact)
        form_data.add_field('department', department)

        # 添加密码（如果提供了）
        if password:
            form_data.add_field('password', password)

        # 准备主文件
        form_data.add_field('file', file_content, filename=file_name, content_type='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet')

        # 准备附件
        if attachments:
            for att_name, att_content in attachments:
                form_data.add_field('attachments', att_content, filename=att_name)

        start_time = time.time()
        try:
            timeout = aiohttp.ClientTimeout(total=self.config.timeout)
            async with aiohttp.ClientSession(timeout=timeout) as session:
                async with session.post(url, data=form_data) as response:
                    duration = time.time() - start_time

                    if response.status == 200:
                        result = await response.json()
                        return True, result, duration
                    else:
                        text = await response.text()
                        return False, {'error': f'HTTP {response.status}', 'detail': text}, duration
        except Exception as e:
            duration = time.time() - start_time
            return False, {'error': str(e)}, duration