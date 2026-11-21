import { SaveIcon } from 'lucide-react';
import React from "react";
import { FormProvider, useForm } from 'react-hook-form';
import type { SettingsMutableDTO } from '../data/sdk/model';
import { useSettingsEdit, useSettingsGet } from '../data/sdk/settings/settings.gen';
import { withErrorCatcher } from '../error/with-error-catcher';
import { Route } from '../routes/settings';
import { SettingsAboutLeft } from '../settings/about/settings-about-left';
import { SettingsAboutRight } from '../settings/about/settings-about-right';
import { SettingsBackupLeft } from '../settings/backup/settings-backup-left';
import { SettingsBackupRight } from '../settings/backup/settings-backup-right';
import { SettingsCloudAccount } from '../settings/cloud/settings-cloud-account';
import { SettingsCloudInfo } from '../settings/cloud/settings-cloud-info';
import { SettingsExternalLeft } from '../settings/external/settings-external-left';
import { SettingsExternalRight } from '../settings/external/settings-external-right';
import { SettingsMainLeft } from '../settings/main/settings-main-left';
import { SettingsMainRight } from '../settings/main/settings-main-right';
import { SettingsMainSystem } from '../settings/main/settings-main-system';
import { useTranslate } from '../translate/i18n';
import { UIButton } from '../ui/form/button/ui-button';
import { UISettingsContent } from '../ui/settings/ui-settings-content';
import { switchUtil } from '../util/switch-util';

export type SettingsFormData = Omit<SettingsMutableDTO, 'savE_GLOBS' | 'pkM_EXTERNAL_GLOBS'> & {
  savE_GLOBS: string;
  pkM_EXTERNAL_GLOBS: string;
};

const settingsToFormData = (settingsMutable: SettingsMutableDTO) => ({
  ...settingsMutable,
  savE_GLOBS: settingsMutable.savE_GLOBS.join('\n'),
  pkM_EXTERNAL_GLOBS: settingsMutable.pkM_EXTERNAL_GLOBS?.join('\n') ?? ''
});

export const SettingsPage: React.FC = withErrorCatcher('default', () => {
  console.log('page settings');

  const { t } = useTranslate();

  const subMenu = Route.useSearch({ select: search => search.subMenu ?? 'main' });

  // const storageHistoryValue = HistoryContext.useValue()[ '/storage' ];

  // const desktopMessage = useDesktopMessage();

  const settingsQuery = useSettingsGet();
  const settingsMutation = useSettingsEdit();

  const settings = settingsQuery.data?.data;
  const settingsMutable = settings?.settingsMutable;

  const defaultValue = React.useMemo((): SettingsFormData | undefined => settingsMutable
    && settingsToFormData(settingsMutable), [ settingsMutable ]);

  const form = useForm<SettingsFormData>({
    defaultValues: defaultValue,
  });

  if (!settingsMutable) {
    return null;
  }

  const submit = form.handleSubmit(async (data) => {
    const result = await settingsMutation.mutateAsync({
      data: {
        ...data,
        savE_GLOBS: data.savE_GLOBS.split('\n').map(value => value.trim()).filter(Boolean),
        pkM_EXTERNAL_GLOBS: data.pkM_EXTERNAL_GLOBS.split('\n').map(value => value.trim()).filter(Boolean)
      },
    });

    const nextSettingsMutable = result.data.settings?.settingsMutable;
    if (nextSettingsMutable)
      form.reset(settingsToFormData(nextSettingsMutable), { keepDefaultValues: false });
  });

  const { title, description, content } = switchUtil(subMenu, {
    main: () => ({
      title: t('settings.sub.main'),
      description: undefined,
      content: <>
        <SettingsMainLeft />
        <SettingsMainRight />
        <SettingsMainSystem />
      </>,
    }),
    "external-pkms": () => ({
      title: t('settings.sub.external'),
      description: t('settings.external.1.description'),
      content: <>
        <SettingsExternalLeft />
        <SettingsExternalRight />
      </>,
    }),
    backups: () => ({
      title: t('settings.sub.backups'),
      description: undefined,
      content: <>
        <SettingsBackupRight />
        <SettingsBackupLeft />
      </>,
    }),
    cloud: () => ({
      title: t('settings.sub.cloud'),
      description: t('cloud.description'),
      content: <>
        <SettingsCloudAccount />
        <SettingsCloudInfo />
      </>,
    }),
    about: () => ({
      title: t('settings.sub.about'),
      description: t('settings.about.1.description'),
      content: <>
        <SettingsAboutLeft />
        <SettingsAboutRight />
      </>,
    }),
  })();

  return <FormProvider {...form}>
    <UISettingsContent
      key={subMenu}
      onSubmit={submit}
      title={title}
      description={description}
      bottom={subMenu !== 'about' && subMenu !== 'cloud' && <>
        <UIButton
          name='cancel'
          controlLabel={t('action.cancel')}
          onClick={() => form.reset(defaultValue)}
          disabled={!form.formState.isDirty}
        >
          {t('action.cancel')}
        </UIButton>

        <UIButton
          name='submit'
          controlLabel={t('action.submit')}
          type='submit'
          variant='filled'
          leftSection={<SaveIcon />}
          loading={settingsMutation.isPending}
          disabled={settings?.demoMode || !form.formState.isDirty}
        >
          {t('action.submit')}
        </UIButton>
      </>}
    >
      {content}
    </UISettingsContent>
  </FormProvider>;
});
