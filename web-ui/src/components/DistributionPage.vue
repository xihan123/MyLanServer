<template>
  <div class="container">
    <button aria-label="切换深色/浅色模式" class="theme-toggle" @click="toggleTheme">
      {{ theme === 'dark' ? '☀️ 浅色模式' : '🌙 深色模式' }}
    </button>

    <div class="header">
      <h1>{{ formTitle }}</h1>
      <p>{{ formDescription }}</p>
    </div>

    <div v-if="errorMessage" aria-live="assertive" class="error show" role="alert">{{ errorMessage }}</div>
    <div v-if="schemaError" aria-live="assertive" class="error show" role="alert">{{ schemaError }}</div>
    <div v-if="submitError" aria-live="assertive" class="error show" role="alert">{{ submitError }}</div>
    <div v-if="submitSuccess" aria-live="polite" class="success show" role="status">{{ submitSuccess }}</div>

    <div v-if="taskInfo" class="info">
      <strong>任务信息：</strong>
      <div class="margin-top-sm">
        <div v-if="taskInfo.title" class="task-title">{{ taskInfo.title }}</div>
        <div v-if="taskInfo.description" class="task-description">{{ taskInfo.description }}</div>
        <div v-if="!taskInfo.isActive" class="task-status status-inactive">任务状态：已关闭</div>
        <div v-if="taskInfo.maxLimit" :class="{ 'limit-reached': taskInfo.isLimitReached }">
          提交进度：{{ taskInfo.currentCount }}/{{ taskInfo.maxLimit }} 份
        </div>
        <div v-if="taskInfo.expiryDate" :class="{ expired: taskInfo.isExpired }">
          截止时间：{{ new Date(taskInfo.expiryDate).toLocaleString('zh-CN') }}
        </div>
        <div v-if="taskInfo.hasPassword">此任务需要访问密码</div>
        <div v-if="!taskInfo.isActive" class="status-warning inactive-warning">
          ⚠️ 此任务已关闭，无法提交表单
        </div>
        <div v-if="taskInfo.isExpired" class="status-warning expired-warning">
          ⚠️ 此任务已过期，无法提交表单
        </div>
        <div v-if="taskInfo.isLimitReached" class="status-warning limit-warning">
          ⚠️ 此任务已达到提交上限，无法继续提交
        </div>
      </div>
    </div>

    <form @submit.prevent="handleSubmit">
      <div v-if="!isTaskInactive" class="form-section">
        <h2>访问验证</h2>
        <div class="form-group">
          <label for="accessPassword">访问密码</label>
          <input id="accessPassword" v-model="password" placeholder="请输入访问密码（如需要）" type="password"/>
        </div>
        <button v-if="taskInfo?.hasPassword && !schemaLoaded" class="btn btn-secondary" type="button"
                @click="() => loadSchema()">
          验证密码并加载表单
        </button>
      </div>

      <div v-if="schemaLoaded" class="form-section">
        <h2>提交人信息</h2>
        <div class="form-group">
          <label for="submitterName">姓名 <span class="required">*</span></label>
          <input id="submitterName" v-model="formData.submitterName" placeholder="请输入您的姓名" required type="text"/>
        </div>
        <div class="form-group">
          <label for="contactInfo">联系方式 <span class="required">*</span></label>
          <input id="contactInfo" v-model="formData.contact" maxlength="11" minlength="4" placeholder="4-11位字符" required
                 type="text"/>
        </div>
        <div class="form-group">
          <label for="department">所属单位/部门 <span class="required">*</span></label>
          <DepartmentSelector id="department" v-model="formData.department"/>
        </div>
      </div>

      <div v-if="schemaLoaded && columns.length" class="form-section">
        <h2>表单内容</h2>
        <div v-for="col in columns" :key="col.name" class="form-group">
          <label :for="`field-${col.name}`">
            {{ col.name }}
            <span v-if="col.required" class="required">*</span>
          </label>
          <select
              v-if="col.type === '双选框(是/否)'"
              :id="`field-${col.name}`"
              v-model="formData.fields[col.name]"
              :required="col.required"
          >
            <option value="">请选择</option>
            <option value="true">是</option>
            <option value="false">否</option>
          </select>
          <input
              v-else-if="col.type === '数字'"
              :id="`field-${col.name}`"
              v-model="formData.fields[col.name]"
              :placeholder="col.description || `请输入${col.name}`"
              :required="col.required"
              type="number"
          />
          <input
              v-else
              :id="`field-${col.name}`"
              v-model="formData.fields[col.name]"
              :placeholder="col.description || `请输入${col.name}`"
              :required="col.required"
              type="text"
          />
        </div>
      </div>

      <div v-if="schemaLoaded && attachments.length > 0" class="form-section">
        <h2>附件下载</h2>
        <div v-if="attachmentDownloadDescription" class="form-info">{{ attachmentDownloadDescription }}</div>
        <div class="attachments-list">
          <div v-for="attachment in attachments" :key="attachment.id" class="attachment-item">
            <div class="attachment-info">
              <div :title="attachment.fileName" class="attachment-name">
                {{ attachment.displayName || attachment.fileName }}
              </div>
              <div class="attachment-meta">
                {{ formattedFileSize(attachment.fileSize) }} · {{ formatDate(attachment.uploadDate) }}
              </div>
            </div>
            <button class="attachment-download-btn" type="button" @click="downloadAttachment(attachment.id)">
              下载
            </button>
          </div>
        </div>
      </div>

      <div v-if="schemaLoaded && taskInfo?.allowAttachmentUpload" class="form-section">
        <h2>附件上传</h2>
        <div class="form-group">
          <label>附件上传（可选）</label>
          <div
              class="upload-area"
              role="button"
              tabindex="0"
              @click="fileInputRef?.click()"
              @dragover.prevent="handleDragOver"
              @dragleave.prevent="handleDragLeave"
              @drop.prevent="handleDrop"
              @keypress.enter="fileInputRef?.click()"
              @keypress.space.prevent="fileInputRef?.click()"
          >
            <div class="upload-icon">📎</div>
            <div class="upload-hint">点击选择附件或拖拽附件到此处</div>
            <input
                ref="fileInputRef"
                :accept="attachmentUpload.getAcceptAttribute()"
                class="hidden"
                multiple
                type="file"
                @change="handleFileSelect"
            />
            <div v-if="selectedFiles.length > 0" class="file-list">
              <div v-for="file in selectedFiles" :key="file.id" class="file-item">
                <div :title="file.file.name" class="file-item-name">{{ file.file.name }}</div>
                <div class="file-item-size">{{ attachmentUpload.formatFileSize(file.file.size) }}</div>
                <div class="file-item-remove" title="移除文件" @click="removeFile(file.id)">×</div>
              </div>
            </div>
          </div>
          <div class="upload-hint">{{ attachmentUpload.getUploadHint() }}</div>
        </div>
      </div>

      <button
          v-if="schemaLoaded && !isTaskInactive"
          :disabled="isSubmitting || !isValid"
          class="btn"
          type="submit"
      >
        <span v-if="isSubmitting" class="spinner"></span>
        {{ isSubmitting ? '提交中...' : '提交' }}
      </button>
    </form>
  </div>
</template>

<script lang="ts" setup>
import {computed, onUnmounted, ref, watch} from 'vue';
import {useUrlSearchParams} from '@vueuse/core';
import {useTheme} from '../composables/useTheme';
import {useTaskInfo} from '../composables/useTaskInfo';
import {useDistributionForm} from '../composables/useDistributionForm';
import {useAttachmentDownload} from '../composables/useAttachmentDownload';
import {useAttachmentUpload} from '../composables/useAttachmentUpload';
import DepartmentSelector from './DepartmentSelector.vue';

const {theme, toggleTheme} = useTheme();
const params = useUrlSearchParams('history');
const slug = computed(() => params.slug as string || '');

const {taskInfo, errorMessage} = useTaskInfo(slug.value);
const {
  password,
  schemaLoaded,
  formTitle,
  formDescription,
  columns,
  formData,
  isValid,
  isSubmitting,
  schemaError,
  submitError,
  loadSchema,
  submitForm,
  setAttachmentFiles,
  clearSchemaError,
  clearSubmitError
} = useDistributionForm(slug.value);

const {
  attachments,
  attachmentDownloadDescription,
  loadAttachments,
  downloadAttachment,
  formattedFileSize,
  formatDate
} = useAttachmentDownload(slug.value, () => password.value);

const attachmentUpload = useAttachmentUpload();
const {selectedFiles} = attachmentUpload;
const fileInputRef = ref<HTMLInputElement>();

// timeout ID 管理，防止内存泄漏
const schemaErrorTimeout = ref<ReturnType<typeof setTimeout> | null>(null);
const submitErrorTimeout = ref<ReturnType<typeof setTimeout> | null>(null);

const submitSuccess = ref('');

// 计算任务是否处于不可逆状态
const isTaskInactive = computed(() => {
  if (!taskInfo.value) return false;
  return !taskInfo.value.isActive || taskInfo.value.isExpired || taskInfo.value.isLimitReached;
});

watch(taskInfo, (info) => {
  if (info?.taskType !== 1) {
    window.location.href = `/task.html?slug=${slug.value}`;
  } else if (!info.hasPassword) {
    loadSchema();
  }

  // 更新允许的文件扩展名
  if (info?.allowedExtensions && info.allowedExtensions.length > 0) {
    attachmentUpload.updateAllowedExtensions(info.allowedExtensions);
  }
});

// 当 schema 加载成功后，加载附件
watch(schemaLoaded, (loaded) => {
  if (loaded) {
    loadAttachments();
  }
});

// 自动清除schema错误提示
watch(schemaError, (newError) => {
  if (newError) {
    if (schemaErrorTimeout.value) {
      clearTimeout(schemaErrorTimeout.value);
    }
    schemaErrorTimeout.value = window.setTimeout(() => {
      clearSchemaError();
      schemaErrorTimeout.value = null;
    }, 5000);
  }
});

// 自动清除提交错误提示
watch(submitError, (newError) => {
  if (newError) {
    if (submitErrorTimeout.value) {
      clearTimeout(submitErrorTimeout.value);
    }
    submitErrorTimeout.value = window.setTimeout(() => {
      clearSubmitError();
      submitErrorTimeout.value = null;
    }, 5000);
  }
});

// 拖拽事件处理
const handleDragOver = (e: DragEvent) => {
  e.preventDefault();
};

const handleDragLeave = (e: DragEvent) => {
  e.preventDefault();
};

const handleDrop = (e: DragEvent) => {
  e.preventDefault();
  const files = e.dataTransfer?.files;
  if (files) {
    try {
      attachmentUpload.selectFiles(files);
    } catch (err: unknown) {
      const errorMessage = err instanceof Error ? err.message : '文件选择失败';
      clearSchemaError();
      setTimeout(() => {
        schemaError.value = errorMessage;
      }, 100);
    }
  }
};

const handleFileSelectionError = (err: unknown) => {
  const errorMessage = err instanceof Error ? err.message : '文件选择失败';
  clearSchemaError();
  if (schemaErrorTimeout.value) {
    clearTimeout(schemaErrorTimeout.value);
  }
  schemaErrorTimeout.value = window.setTimeout(() => {
    schemaError.value = errorMessage;
    schemaErrorTimeout.value = null;
  }, 100);
};

const handleFileSelect = (e: Event) => {
  const files = (e.target as HTMLInputElement).files;
  if (files) {
    try {
      attachmentUpload.selectFiles(files);
    } catch (err: unknown) {
      handleFileSelectionError(err);
    }
  }
};

const removeFile = (id: string) => {
  attachmentUpload.removeFile(id);
};

const handleSubmit = async () => {
  submitSuccess.value = '';
  const files = selectedFiles.value.map(f => f.file);
  setAttachmentFiles(files);
  const result = await submitForm();
  if (!submitError.value && result) {
    submitSuccess.value = result.message || '提交成功';
    // 清除附件列表
    attachmentUpload.reset();
  }
};

// 组件卸载时清理所有 timeout
onUnmounted(() => {
  if (schemaErrorTimeout.value) {
    clearTimeout(schemaErrorTimeout.value);
  }
  if (submitErrorTimeout.value) {
    clearTimeout(submitErrorTimeout.value);
  }
});
</script>
