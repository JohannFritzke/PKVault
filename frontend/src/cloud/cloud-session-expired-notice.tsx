import { Anchor, Badge, Group, Loader, Paper, Stack, Text, ThemeIcon } from '@mantine/core';
import { useQueryClient } from '@tanstack/react-query';
import { KeyRoundIcon, LogInIcon } from 'lucide-react';
import React from 'react';
import { useCloudLogin, useCloudStatus } from '../data/sdk/cloud/cloud.gen';
import { CloudLoginState } from '../data/sdk/model';
import { useTranslate } from '../translate/i18n';
import { cloudProviderLabel } from './cloud-provider-label';
import { UIButton } from '../ui/form/button/ui-button';

/**
 * The server refused the saved session (too long without opening the app, or everyone was logged out).
 * The account is still known: "Log in again" uses the same provider, so it continues where it was.
 */
export const CloudSessionExpiredNotice: React.FC = () => {
    const { t } = useTranslate();
    const queryClient = useQueryClient();
    const loginMutation = useCloudLogin();

    const statusQuery = useCloudStatus({
        query: {
            refetchInterval: query => query.state.data?.data.loginState === CloudLoginState.WAITING_BROWSER ? 1500 : false,
        },
    });
    const status = statusQuery.data?.data;
    const [ authUrl, setAuthUrl ] = React.useState<string>();

    const loggedIn = !!status?.loggedIn;
    React.useEffect(() => {
        if (loggedIn)
            void queryClient.invalidateQueries();
    }, [ loggedIn, queryClient ]);

    if (!status?.sessionExpired || !status.provider)
        return null;

    const provider = status.provider;
    const waiting = status.loginState === CloudLoginState.WAITING_BROWSER;

    const login = async () => {
        const result = await loginMutation.mutateAsync({ data: { provider } });
        setAuthUrl(result.data.authUrl);
        if (!result.data.browserOpened)
            window.open(result.data.authUrl, '_blank', 'noopener');
        await statusQuery.refetch();
    };

    return <Paper withBorder p='lg' radius='md'>
        <Stack gap='md'>
            <Group gap='sm' wrap='nowrap'>
                <ThemeIcon variant='light' color='yellow' radius='xl' size='lg'>
                    <KeyRoundIcon />
                </ThemeIcon>
                <Text fw={700} size='lg'>{t('cloud.session.expired.title')}</Text>
                <Badge variant='light' color='yellow'>{t('cloud.session.expired.badge')}</Badge>
            </Group>
            <Text size='sm'>{t('cloud.session.expired.description')}</Text>
            <Text size='sm' c='dimmed'>{t('cloud.session.expired.safe')}</Text>

            {waiting && <Group gap='xs'>
                <Loader size='xs' />
                <Text size='sm'>
                    {t('cloud.login.waiting', { provider: cloudProviderLabel(provider) })}
                    {authUrl && <>
                        {' '}
                        <Anchor href={authUrl} target='_blank' rel='noopener'>{t('cloud.login.open-link')}</Anchor>
                    </>}
                </Text>
            </Group>}

            <Group justify='flex-end'>
                <UIButton
                    name='cloud-relogin'
                    controlLabel={t('cloud.session.relogin', { provider: cloudProviderLabel(provider) })}
                    onClick={login}
                    loading={loginMutation.isPending}
                    leftSection={<LogInIcon />}
                    variant='filled'
                >
                    {t('cloud.session.relogin', { provider: cloudProviderLabel(provider) })}
                </UIButton>
            </Group>
        </Stack>
    </Paper>;
};
