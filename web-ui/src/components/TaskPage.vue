<template>
  <div class="container">
    <button aria-label="切换深色/浅色模式" class="theme-toggle" @click="toggleTheme">
      {{ theme === 'dark' ? '☀️ 浅色模式' : '🌙 深色模式' }}
    </button>

    <div class="header">
      <h1>文件收集系统</h1>
      <p>请选择您要执行的操作</p>
    </div>

    <div v-if="errorMessage" aria-live="assertive" class="error show" role="alert">{{ errorMessage }}</div>

    <div id="actionArea">
      <div
          :class="{ disabled: isTaskInactive }"
          class="action-card"
          role="button"
          tabindex="0"
          @click="!isTaskInactive && (showModal = 'download')"
          @keypress.enter="!isTaskInactive && (showModal = 'download')"
          @keypress.space="!isTaskInactive && (showModal = 'download')"
      >
        <h3>下载模板</h3>
        <p>下载 Excel 模板文件，填写后上传</p>
      </div>
      <div
          :class="{ disabled: isTaskInactive || taskInfo?.isLimitReached }"
          class="action-card"
          role="button"
          tabindex="0"
          @click="!isTaskInactive && !taskInfo?.isLimitReached && (showModal = 'upload')"
          @keypress.enter="!isTaskInactive && !taskInfo?.isLimitReached && (showModal = 'upload')"
          @keypress.space="!isTaskInactive && !taskInfo?.isLimitReached && (showModal = 'upload')"
      >
        <h3>上传文件</h3>
        <p>提交已填写的 Excel 文件</p>
      </div>
    </div>

    <div v-if="taskInfo" class="info">
      <strong>任务信息：</strong>
      <div class="margin-top-sm">
        <div v-if="taskInfo.title" class="task-title">{{ taskInfo.title }}</div>
        <div v-if="taskInfo.description" class="task-description">{{ taskInfo.description }}</div>
        <div v-if="!taskInfo.isActive" class="task-status status-inactive">任务状态：已关闭</div>
        <div v-if="taskInfo.maxLimit" :class="{ 'limit-reached': taskInfo.isLimitReached }">
          提交上限：{{ taskInfo.currentCount }}/{{ taskInfo.maxLimit }} 份
        </div>
        <div v-if="taskInfo.expiryDate" :class="{ expired: taskInfo.isExpired }">
          截止时间：{{ new Date(taskInfo.expiryDate).toLocaleString('zh-CN') }}
        </div>
        <div v-if="taskInfo.hasPassword">此任务需要访问密码</div>
        <div v-if="!taskInfo.isActive" class="status-warning inactive-warning">
          ⚠️ 此任务已关闭，无法提交文件
        </div>
        <div v-if="taskInfo.isExpired" class="status-warning expired-warning">
          ⚠️ 此任务已过期，无法提交文件
        </div>
        <div v-if="taskInfo.isLimitReached" class="status-warning limit-warning">
          ⚠️ 此任务已达到提交上限，无法继续提交
        </div>
      </div>
    </div>

    <!-- 下载模态框 -->
    <div v-if="showModal === 'download'" aria-labelledby="downloadModalTitle" aria-modal="true" class="modal show"
         role="dialog" @click.self="showModal = null">
      <div class="modal-content">
        <div class="modal-header">
          <h2 id="downloadModalTitle">下载模板</h2>
          <button aria-label="关闭" class="close-btn" @click="showModal = null">&times;</button>
        </div>
        <div v-if="downloadError" aria-live="assertive" class="error show" role="alert">{{ downloadError }}</div>
        <form @submit.prevent="handleDownload">
          <div class="form-group">
            <label for="downloadPassword">访问密码</label>
            <input id="downloadPassword" v-model="downloadPassword" placeholder="请输入密码（如需要）" type="password"/>
          </div>
          <button :disabled="isDownloading" class="btn" type="submit">
            {{ isDownloading ? '下载中...' : '开始下载' }}
          </button>
        </form>
      </div>
    </div>

    <!-- 上传模态框 -->
    <div v-if="showModal === 'upload'" aria-labelledby="uploadModalTitle" aria-modal="true" class="modal show" role="dialog"
         @click.self="showModal = null">
      <div class="modal-content">
        <div class="modal-header">
          <h2 id="uploadModalTitle">上传文件</h2>
          <button aria-label="关闭" class="close-btn" @click="showModal = null">&times;</button>
        </div>
        <div v-if="uploadError" aria-live="assertive" class="error show" role="alert">{{ uploadError }}</div>
        <div v-if="uploadSuccess" aria-live="polite" class="success show" role="status">{{ uploadSuccess }}</div>
        <form @submit.prevent="handleUpload">
          <div class="form-group">
            <label for="submitterName">您的姓名 *</label>
            <input id="submitterName" v-model="uploadForm.name" placeholder="请输入您的姓名" required type="text"/>
          </div>
          <div class="form-group">
            <label for="contact">联系方式 *</label>
            <input id="contact" v-model="uploadForm.contact" maxlength="11" minlength="4" placeholder="4-11位字符" required
                   type="text"/>
          </div>
          <div class="form-group">
            <label for="department">所属单位/部门 *</label>
            <DepartmentSelector id="department" v-model="uploadForm.department"/>
          </div>
          <div class="form-group">
            <label for="uploadPassword">访问密码</label>
            <input id="uploadPassword" v-model="uploadForm.password" placeholder="请输入访问密码（如需要）"
                   type="password"/>
          </div>
          <div class="form-group">
            <label for="fileInput">选择文件 *</label>
            <FileUploadArea
                ref="fileUploadAreaRef"
                :multiple="false"
                acceptAttribute=".xlsx,.xls"
                aria-label="选择文件"
                hint="点击选择文件或拖拽文件到此处"
                icon="📁"
                @update:selectedFiles="handleFileUpdate"
            />
            <div v-if="selectedFile" class="file-info show">
              已选择: {{ selectedFile.name }} ({{ formatFileSize(selectedFile.size) }})
            </div>
          </div>
          <div v-if="taskInfo?.allowAttachmentUpload" class="form-group">
            <label>附件上传（可选）</label>
            <div
                id="attachmentUploadArea"
                class="upload-area"
                role="button"
                tabindex="0"
                @click="attachmentInputRef?.click()"
                @dragover.prevent="handleAttachmentDragOver"
                @dragleave.prevent="handleAttachmentDragLeave"
                @drop.prevent="handleAttachmentDrop"
                @keypress.enter="attachmentInputRef?.click()"
                @keypress.space.prevent="attachmentInputRef?.click()"
            >
              <div class="upload-icon">📎</div>
              <div class="upload-hint">点击选择附件或拖拽附件到此处</div>
              <input
                  id="attachmentInput"
                  ref="attachmentInputRef"
                  :accept="attachmentUpload.getAcceptAttribute()"
                  class="hidden"
                  multiple
                  type="file"
                  @change="handleAttachmentSelect"
              />
              <div v-if="selectedAttachments.length > 0" class="file-list">
                <div v-for="attachment in selectedAttachments" :key="attachment.id" class="file-item">
                  <div :title="attachment.file.name" class="file-item-name">{{ attachment.file.name }}</div>
                  <div class="file-item-size">{{ attachmentUpload.formatFileSize(attachment.file.size) }}</div>
                  <div class="file-item-remove" title="移除文件" @click="removeAttachment(attachment.id)">×</div>
                </div>
              </div>
            </div>
            <div class="upload-hint">{{ attachmentUpload.getUploadHint() }}</div>
          </div>
          <div :class="{ show: isUploading }" class="progress-container">
            <div :style="{ width: uploadProgress + '%' }" class="progress-bar"></div>
          </div>
          <div v-if="isUploading" class="progress-text">上传中... {{ uploadProgress }}%</div>
          <button :disabled="isUploading || !isUploadValid" class="btn" type="submit">
            {{ isUploading ? '上传中...' : '开始上传' }}
          </button>
        </form>
      </div>
    </div>
  </div>
</template>

<script lang="ts" setup>
import {computed, onUnmounted, ref, watch} from 'vue';
import {useUrlSearchParams} from '@vueuse/core';
import {useTheme} from '../composables/useTheme';
import {useTaskInfo} from '../composables/useTaskInfo';
import {useFileUpload} from '../composables/useFileUpload';
import {useTemplateDownload} from '../composables/useTemplateDownload';
import {useAttachmentUpload} from '../composables/useAttachmentUpload';
import {formatFileSize} from '../utils/api';
import DepartmentSelector from './DepartmentSelector.vue';
import FileUploadArea from './FileUploadArea.vue';

const {theme, toggleTheme} = useTheme();
const params = useUrlSearchParams('history');
const slug = computed(() => params.slug as string || '');

const {taskInfo, errorMessage} = useTaskInfo(slug.value);
const {
  password: downloadPassword,
  isLoading: isDownloading,
  error: downloadError,
  download
} = useTemplateDownload(slug.value);
const {
  selectedFile,
  form: uploadForm,
  isValid: isUploadValid,
  isLoading: isUploading,
  error: uploadError,
  upload,
  setAttachmentFiles,
  clearError: clearUploadError,
  uploadProgress
} = useFileUpload(slug.value);
const attachmentUpload = useAttachmentUpload();
const {selectedFiles: selectedAttachments} = attachmentUpload;

const showModal = ref<'download' | 'upload' | null>(null);
const uploadSuccess = ref('');
const attachmentInputRef = ref<HTMLInputElement>();

// timeout ID 管理，防止内存泄漏
const downloadErrorTimeout = ref<ReturnType<typeof setTimeout> | null>(null);
const uploadErrorTimeout = ref<ReturnType<typeof setTimeout> | null>(null);
const progressInterval = ref<ReturnType<typeof setInterval> | null>(null);

const handleFileUpdate = (files: File[]) => {
  selectedFile.value = files[0] || null;
};

// 计算任务是否处于不可逆状态
const isTaskInactive = computed(() => {
  if (!taskInfo.value) return false;
  return !taskInfo.value.isActive || taskInfo.value.isExpired;
});

watch(taskInfo, (info) => {
  if (info?.taskType !== 0) {
    window.location.href = `/distribution.html?slug=${slug.value}`;
  }

  // 更新允许的文件扩展名
  if (info?.allowedExtensions && info.allowedExtensions.length > 0) {
    attachmentUpload.updateAllowedExtensions(info.allowedExtensions);
  }
});

// 自动清除下载错误提示
watch(downloadError, (newError) => {
  if (newError) {
    if (downloadErrorTimeout.value) {
      clearTimeout(downloadErrorTimeout.value);
    }
    downloadErrorTimeout.value = window.setTimeout(() => {
      downloadError.value = '';
      downloadErrorTimeout.value = null;
    }, 5000);
  }
});

// 自动清除上传错误提示
watch(uploadError, (newError) => {
  if (newError) {
    if (uploadErrorTimeout.value) {
      clearTimeout(uploadErrorTimeout.value);
    }
    uploadErrorTimeout.value = window.setTimeout(() => {
      clearUploadError();
      uploadErrorTimeout.value = null;
    }, 5000);
  }
});

const handleDownload = async () => {
  await download();
  if (!downloadError.value) {
    setTimeout(() => showModal.value = null, 2000);
  }
};

// 提取重复的错误处理逻辑
const handleFileSelectionError = (err: unknown) => {
  const errorMessage = err instanceof Error ? err.message : '文件选择失败';
  clearUploadError();
  if (uploadErrorTimeout.value) {
    clearTimeout(uploadErrorTimeout.value);
  }
  uploadErrorTimeout.value = window.setTimeout(() => {
    uploadError.value = errorMessage;
    uploadErrorTimeout.value = null;
  }, 100);
};

// 附件拖拽事件处理
const handleAttachmentDragOver = (e: DragEvent) => {
  e.preventDefault();
};

const handleAttachmentDragLeave = (e: DragEvent) => {
  e.preventDefault();
};

const handleAttachmentDrop = (e: DragEvent) => {
  e.preventDefault();
  const files = e.dataTransfer?.files;
  if (files) {
    try {
      attachmentUpload.selectFiles(files);
    } catch (err: unknown) {
      handleFileSelectionError(err);
    }
  }
};

const handleAttachmentSelect = (e: Event) => {
  const files = (e.target as HTMLInputElement).files;
  if (files) {
    try {
      attachmentUpload.selectFiles(files);
    } catch (err: unknown) {
      handleFileSelectionError(err);
    }
  }
};

const removeAttachment = (id: string) => {
  attachmentUpload.removeFile(id);
};

const handleUpload = async () => {
  uploadSuccess.value = '';
  uploadProgress.value = 0;

  // 设置附件文件
  const attachmentFiles = selectedAttachments.value.map(f => f.file);
  setAttachmentFiles(attachmentFiles);

  try {
    const result = await upload();
    uploadProgress.value = 100;

    if (!uploadError.value && result) {
      uploadSuccess.value = '上传成功！文件名: ' + result.filename;
      // 重置表单而不是刷新页面
      setTimeout(() => {
        uploadSuccess.value = '';
        selectedFile.value = null;
        uploadForm.value = {name: '', contact: '', department: '', password: ''};
        attachmentUpload.reset();
        showModal.value = null;
      }, 2000);
    }
  } catch (err) {
    uploadProgress.value = 0;
  }
};

// 组件卸载时清理所有 timeout 和 interval
onUnmounted(() => {
  if (downloadErrorTimeout.value) {
    clearTimeout(downloadErrorTimeout.value);
  }
  if (uploadErrorTimeout.value) {
    clearTimeout(uploadErrorTimeout.value);
  }
  if (progressInterval.value) {
    clearInterval(progressInterval.value);
  }
});
</script>
