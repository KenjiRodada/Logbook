-- Logbook: history of aid actually provided to a household or a person.
-- Execute in Aiven MySQL after checking the selected database is defaultdb.
-- Does not insert synthetic/demo records and does not alter existing records.

CREATE TABLE IF NOT EXISTS assistance_history (
    assistance_id BIGINT NOT NULL AUTO_INCREMENT,
    house_id BIGINT NOT NULL,
    member_id BIGINT NULL,
    assistance_type VARCHAR(50) NOT NULL,
    program_name VARCHAR(150) NOT NULL,
    description TEXT NULL,
    amount DECIMAL(12,2) NULL,
    assistance_date DATE NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'ได้รับแล้ว',
    provider VARCHAR(150) NULL,
    created_by INT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (assistance_id),
    KEY ix_assistance_history_house_date (house_id, assistance_date),
    KEY ix_assistance_history_member_date (member_id, assistance_date),
    CONSTRAINT fk_assistance_history_house FOREIGN KEY (house_id)
        REFERENCES t_house (house_id),
    CONSTRAINT fk_assistance_history_member FOREIGN KEY (member_id)
        REFERENCES t_house_member (member_id),
    CONSTRAINT fk_assistance_history_user FOREIGN KEY (created_by)
        REFERENCES web_users (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
