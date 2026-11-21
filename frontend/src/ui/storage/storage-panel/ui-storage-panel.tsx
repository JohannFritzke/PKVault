import { Box, Card, type CardProps } from '@mantine/core';
import clsx from 'clsx';
import React from 'react';
import { useStorageMode } from '../../inventory/context/storage-mode-context';
import { getScrollPadding } from '../../scrollbar-width/util/get-scroll-padding';
import classes from './ui-storage-panel.module.css';

export type UIStoragePanelProps = {
    gameTabs: React.ReactNode;
    header: React.ReactNode;
    children: React.ReactNode;
    footer: React.ReactNode;
    backgroundImageUrl?: string;
} & CardProps;

export const UIStoragePanel: React.FC<UIStoragePanelProps> = ({ gameTabs, header, children, footer, backgroundImageUrl, className, ...rest }) => {
    const storageMode = useStorageMode().state;

    return <Card
        withBorder
        radius='lg'
        shadow='none'
        h='100%'
        className={clsx(classes.panel, className)}
        style={{
            borderColor: storageMode === 'inventory'
                ? 'var(--mantine-color-violet-4)'
                : undefined,
        }}
        {...rest}
    >
        <Card.Section mah='100%' className={classes.tabsSection}>
            {gameTabs}
        </Card.Section>

        {header && <Box my='sm' mah='100%' className={classes.header}>
            {header}
        </Box>}

        {children && <Card.Section inheritPadding py='lg' withBorder className={classes.content} style={{
            paddingRight: getScrollPadding('md'),
            backgroundImage: backgroundImageUrl && `url("${backgroundImageUrl}")`,
        }}>
            {children}
        </Card.Section>}

        {footer && <Card.Section inheritPadding withBorder className={classes.footer}>
            {footer}
        </Card.Section>}
    </Card>;
};
