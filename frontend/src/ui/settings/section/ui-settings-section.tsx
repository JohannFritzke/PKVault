import { Card } from '@mantine/core';
import { clsx } from 'clsx';
import type React from 'react';
import classes from '../ui-settings.module.css';

type UISettingsSectionProps = {
    title?: React.ReactNode;
    description?: React.ReactNode;
    actions?: React.ReactNode;
    /**
     * Pads the body. Disable it for bodies made of rows or tables, which pad themselves.
     */
    padded?: boolean;
    children?: React.ReactNode;
};

/**
 * Bordered settings card: optional header (title, description, actions) and a body.
 */
export const UISettingsSection: React.FC<UISettingsSectionProps> = ({ title, description, actions, padded, children }) => {
    const hasHeader = Boolean(title || description || actions);

    return <Card className={classes.section} p={0} radius='lg' shadow='none'>
        {hasHeader && <div className={classes.sectionHeader}>
            <div className={classes.sectionHeading}>
                {title && <h2 className={classes.sectionTitle}>{title}</h2>}
                {description && <div className={classes.sectionDescription}>{description}</div>}
            </div>

            {actions && <div className={classes.sectionActions}>
                {actions}
            </div>}
        </div>}

        {children && <div className={clsx(classes.sectionBody, padded && classes.sectionBodyPadded)}>
            {children}
        </div>}
    </Card>;
};
