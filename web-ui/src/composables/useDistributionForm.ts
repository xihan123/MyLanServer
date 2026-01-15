import {ref, computed} from 'vue';
import {useAsyncState} from '@vueuse/core';
import {fetchWithTimeout, getSafeErrorMessage} from '../utils/api';

export interface FormColumn {
    name: string;
    type: string;
    required: boolean;
    description?: string;
}

export interface FormSchema {
    title: string;
    description: string;
    columns: FormColumn[];
}

export function useDistributionForm(slug: string) {
    const password = ref('');
    const schemaLoaded = ref(false);
    const formTitle = ref('在线填表');
    const formDescription = ref('请填写以下信息');
    const columns = ref<FormColumn[]>([]);
    const attachmentFiles = ref<File[]>([]);

    const formData = ref({
        submitterName: '',
        contact: '',
        department: '',
        fields: {} as Record<string, any>
    });

    const {execute: loadSchema, isLoading: isLoadingSchema, error: schemaError} = useAsyncState(
        async () => {
            const headers: Record<string, string> = {};
            if (password.value) headers['X-Password'] = password.value;

            const response = await fetchWithTimeout(`/api/distribution/${slug}/schema`, {headers});
            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(getSafeErrorMessage(errorData, response.status));
            }

            const schema: FormSchema = await response.json();
            formTitle.value = schema.title || '在线填表';
            formDescription.value = '请填写以下信息，所有带 * 的字段为必填项';
            columns.value = schema.columns || [];
            schemaLoaded.value = true;
        },
        null,
        {immediate: false}
    );

    const {execute: submitForm, isLoading: isSubmitting, error: submitError} = useAsyncState(
        async () => {
            const submitData = new FormData();
            submitData.append('name', formData.value.submitterName);
            submitData.append('contact', formData.value.contact);
            submitData.append('department', formData.value.department);
            submitData.append('jsonData', JSON.stringify(formData.value.fields));
            if (password.value) submitData.append('password', password.value);

            // 添加附件文件（支持多文件）
            if (attachmentFiles.value && attachmentFiles.value.length > 0) {
                attachmentFiles.value.forEach((file) => {
                    submitData.append('attachment', file);
                });
            }

            const response = await fetchWithTimeout(`/api/distribution/${slug}/submit`, {
                method: 'POST',
                body: submitData
            });

            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(getSafeErrorMessage(errorData, response.status));
            }

            const result = await response.json();
            formData.value = {submitterName: '', contact: '', department: '', fields: {}};
            return result;
        },
        null,
        {immediate: false}
    );

    const setAttachmentFiles = (files: File[]) => {
        attachmentFiles.value = files;
    };

    const clearSchemaError = () => {
        schemaError.value = '';
    };

    const clearSubmitError = () => {
        submitError.value = '';
    };

    const isValid = computed(() =>
        formData.value.submitterName &&
        formData.value.contact &&
        formData.value.department &&
        columns.value.every(col => !col.required || formData.value.fields[col.name])
    );

    return {
        password,
        schemaLoaded,
        formTitle,
        formDescription,
        columns,
        formData,
        isValid,
        isLoadingSchema,
        isSubmitting,
        schemaError,
        submitError,
        loadSchema,
        submitForm,
        setAttachmentFiles,
        clearSchemaError,
        clearSubmitError
    };
}
